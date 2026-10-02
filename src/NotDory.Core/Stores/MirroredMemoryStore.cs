namespace NotDory.Core.Stores
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NotDory.Core.Enums;
    using NotDory.Core.Models;
    using NotDory.Core.Stores.Filesystem;

    /// <summary>
    /// A memory store that writes every memory to a primary store (RecallDB) and, concurrently, to a filesystem mirror
    /// laid out as an Open Knowledge Format bundle in a <c>.okf</c> directory under the scope's target path (typically a
    /// repository root, so the bundle stays out of the way of the repository's own files). The primary store is the system of
    /// record: it serves every search and supplies the store key. The mirror is a git-trackable copy of the memory
    /// bodies. A write fails if either side fails, so the mirror never silently falls behind. Deleting the scope
    /// leaves the mirror files in place, because the target path is often inside a repository the user owns.
    /// </summary>
    public class MirroredMemoryStore : IMemoryStore
    {
        #region Public-Members

        /// <summary>
        /// The directory, under the scope's target path, that holds the mirror bundle.
        /// </summary>
        public const string BundleDirectoryName = ".okf";

        /// <inheritdoc />
        public StoreCapabilities Capabilities
        {
            get
            {
                StoreCapabilities primary = _Primary.Capabilities;
                return new StoreCapabilities
                {
                    SupportsKeyword = primary.SupportsKeyword,
                    SupportsSemantic = primary.SupportsSemantic,
                    SupportsHybrid = primary.SupportsHybrid,
                    RequiresEmbedding = primary.RequiresEmbedding,
                    Description = primary.Description + " Every memory is also mirrored to an Open Knowledge Format bundle on the filesystem."
                };
            }
        }

        /// <summary>
        /// The store that holds the system of record and serves searches.
        /// </summary>
        public IMemoryStore Primary
        {
            get
            {
                return _Primary;
            }
        }

        /// <summary>
        /// The store that receives the filesystem copy.
        /// </summary>
        public IMemoryStore Mirror
        {
            get
            {
                return _Mirror;
            }
        }

        #endregion

        #region Private-Members

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _MirrorLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

        private readonly IMemoryStore _Primary;
        private readonly IMemoryStore _Mirror;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a mirrored store over a primary store and a filesystem mirror.
        /// </summary>
        /// <param name="primary">The primary store (system of record, serves searches).</param>
        /// <param name="mirror">The mirror store; a <see cref="FilesystemMemoryStore"/> unless a test substitutes one.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public MirroredMemoryStore(IMemoryStore primary, IMemoryStore? mirror = null)
        {
            _Primary = primary ?? throw new ArgumentNullException(nameof(primary));
            _Mirror = mirror ?? new FilesystemMemoryStore();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// The directory a scope's mirror bundle is written to: <see cref="BundleDirectoryName"/> under its target path.
        /// </summary>
        /// <param name="scope">The scope being mirrored.</param>
        /// <returns>The bundle directory.</returns>
        /// <exception cref="ArgumentNullException">Thrown when scope is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the scope has no target path.</exception>
        public static string BundlePath(Scope scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (String.IsNullOrWhiteSpace(scope.TargetPath)) throw new InvalidOperationException("Scope '" + scope.Id + "' mirrors to the filesystem but has no target path configured.");
            return Path.Combine(scope.TargetPath!, BundleDirectoryName);
        }

        /// <summary>
        /// The scope the mirror store sees: the same identity, always in the OKF bundle layout, rooted at
        /// <see cref="BundlePath(Scope)"/>.
        /// </summary>
        /// <param name="scope">The scope being mirrored.</param>
        /// <returns>The mirror scope.</returns>
        /// <exception cref="ArgumentNullException">Thrown when scope is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the scope has no target path.</exception>
        public static Scope MirrorScope(Scope scope)
        {
            string bundle = BundlePath(scope);

            return new Scope
            {
                Id = scope.Id,
                TenantId = scope.TenantId,
                Name = scope.Name,
                Description = scope.Description,
                StoreProvider = StoreProviderEnum.Filesystem,
                FilesystemLayout = FilesystemLayoutEnum.OkfBundle,
                TargetPath = bundle
            };
        }

        /// <inheritdoc />
        public async Task EnsureScopeAsync(Scope scope, CancellationToken token = default)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            await Task.WhenAll(
                _Primary.EnsureScopeAsync(scope, token),
                _Mirror.EnsureScopeAsync(MirrorScope(scope), token)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<string> UpsertAsync(Scope scope, Memory memory, IReadOnlyList<MemoryChunk> chunks, CancellationToken token = default)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (memory == null) throw new ArgumentNullException(nameof(memory));

            // The filesystem store records its file path as the memory's store key; it writes to a copy so the primary
            // store, running at the same time, still sees (and replaces) its own key.
            Memory copy = memory.ShallowCopy();
            Task<string> primary = _Primary.UpsertAsync(scope, memory, chunks, token);
            Task mirror = WithMirrorLockAsync(scope, () => _Mirror.UpsertAsync(MirrorScope(scope), copy, chunks, token), token);
            await Task.WhenAll(primary, mirror).ConfigureAwait(false);
            return await primary.ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteAsync(Scope scope, Memory memory, CancellationToken token = default)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (memory == null) throw new ArgumentNullException(nameof(memory));

            Memory copy = memory.ShallowCopy();
            await Task.WhenAll(
                _Primary.DeleteAsync(scope, memory, token),
                WithMirrorLockAsync(scope, () => _Mirror.DeleteAsync(MirrorScope(scope), copy, token), token)).ConfigureAwait(false);
        }

        /// <summary>
        /// Tear down the primary store's content for the scope. The mirror bundle is left on disk: its directory is
        /// usually inside a repository, and deleting a scope should not delete files the user may have committed.
        /// </summary>
        /// <param name="scope">The scope whose content is being removed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task DeleteScopeAsync(Scope scope, CancellationToken token = default)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            return _Primary.DeleteScopeAsync(scope, token);
        }

        /// <inheritdoc />
        public Task DeleteTenantAsync(string tenantId, CancellationToken token = default)
        {
            return _Primary.DeleteTenantAsync(tenantId, token);
        }

        /// <inheritdoc />
        public Task<MemorySearchResult> SearchAsync(Scope scope, MemorySearchQuery query, float[]? queryEmbedding, CancellationToken token = default)
        {
            return _Primary.SearchAsync(scope, query, queryEmbedding, token);
        }

        /// <summary>
        /// Write one memory to the mirror only, for backfilling a mirror turned on after memories already existed.
        /// </summary>
        /// <param name="scope">The scope being mirrored.</param>
        /// <param name="memory">The memory to write.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task UpsertMirrorAsync(Scope scope, Memory memory, CancellationToken token = default)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (memory == null) throw new ArgumentNullException(nameof(memory));

            Memory copy = memory.ShallowCopy();
            List<MemoryChunk> chunks = new List<MemoryChunk> { new MemoryChunk { Ordinal = 0, Text = copy.Body, StartOffset = 0, EndOffset = copy.Body.Length } };
            await WithMirrorLockAsync(scope, () => _Mirror.UpsertAsync(MirrorScope(scope), copy, chunks, token), token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Serialize mirror writes per target directory. Each OKF write regenerates the bundle's index.md from every
        /// file in the directory, so two writes to different memories of the same bundle would otherwise race on it.
        /// </summary>
        private static async Task WithMirrorLockAsync(Scope scope, Func<Task> action, CancellationToken token)
        {
            string key = Path.GetFullPath(MirrorScope(scope).TargetPath!);
            SemaphoreSlim gate = _MirrorLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await action().ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        #endregion
    }
}
