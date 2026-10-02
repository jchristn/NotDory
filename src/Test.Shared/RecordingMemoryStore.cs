namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NotDory.Core.Models;
    using NotDory.Core.Stores;

    /// <summary>
    /// An in-memory <see cref="IMemoryStore"/> that records each call, standing in for RecallDB as the primary store of a
    /// <see cref="MirroredMemoryStore"/> so mirroring can be tested without external services.
    /// </summary>
    public class RecordingMemoryStore : IMemoryStore
    {
        #region Public-Members

        /// <inheritdoc />
        public StoreCapabilities Capabilities { get; } = new StoreCapabilities
        {
            SupportsKeyword = true,
            SupportsSemantic = true,
            SupportsHybrid = true,
            RequiresEmbedding = true,
            Description = "Recording test store."
        };

        /// <summary>
        /// The calls received, in order, as "Method:slug" (or just the method name).
        /// </summary>
        public ConcurrentQueue<string> Calls { get; } = new ConcurrentQueue<string>();

        /// <summary>
        /// The store key each memory was last saved under, by slug.
        /// </summary>
        public ConcurrentDictionary<string, string> Keys { get; } = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// The result every search returns.
        /// </summary>
        public MemorySearchResult SearchResult { get; set; } = new MemorySearchResult();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a recording store.
        /// </summary>
        public RecordingMemoryStore()
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task EnsureScopeAsync(Scope scope, CancellationToken token = default)
        {
            Calls.Enqueue("Ensure");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<string> UpsertAsync(Scope scope, Memory memory, IReadOnlyList<MemoryChunk> chunks, CancellationToken token = default)
        {
            Calls.Enqueue("Upsert:" + memory.Slug);
            string key = "rec:" + memory.Slug;
            Keys[memory.Slug] = key;
            return Task.FromResult(key);
        }

        /// <inheritdoc />
        public Task DeleteAsync(Scope scope, Memory memory, CancellationToken token = default)
        {
            Calls.Enqueue("Delete:" + memory.Slug);
            Keys.TryRemove(memory.Slug, out _);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeleteScopeAsync(Scope scope, CancellationToken token = default)
        {
            Calls.Enqueue("DeleteScope");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeleteTenantAsync(string tenantId, CancellationToken token = default)
        {
            Calls.Enqueue("DeleteTenant");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<MemorySearchResult> SearchAsync(Scope scope, MemorySearchQuery query, float[]? queryEmbedding, CancellationToken token = default)
        {
            Calls.Enqueue("Search");
            return Task.FromResult(SearchResult);
        }

        #endregion
    }
}
