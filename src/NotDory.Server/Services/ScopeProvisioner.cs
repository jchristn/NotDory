namespace NotDory.Server.Services
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NotDory.Core.Database;
    using NotDory.Core.Enums;
    using NotDory.Core.Models;
    using NotDory.Core.Recall;
    using NotDory.Server.Settings;

    /// <summary>
    /// Creates scopes that are usable as created: a RecallDB scope gets the tenant's embedding endpoint and its dimension
    /// when none is named, and the tenant's first cross-encoder as its reranker; the models a scope names are checked for
    /// the job. A new RecallDb scope mirrors to the filesystem unless the request opts out (<see cref="StorageSettings"/>).
    /// Shared by the scope routes and session start.
    /// </summary>
    public class ScopeProvisioner
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly StorageSettings _Storage;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the provisioner.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="storage">Storage settings (the default filesystem mirror); null uses the defaults.</param>
        /// <exception cref="ArgumentNullException">Thrown when database is null.</exception>
        public ScopeProvisioner(DatabaseDriverBase database, StorageSettings? storage = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Storage = storage ?? new StorageSettings();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a scope in a tenant.
        /// </summary>
        /// <param name="tenantId">The tenant.</param>
        /// <param name="scope">The scope to create; its name is required.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="filesystemMirror">Whether the request asked for the filesystem mirror; null when it did not say, so
        /// a RecallDb scope with a targetPath follows <see cref="StorageSettings.MirrorByDefault"/>.</param>
        /// <returns>The created scope, or why it was refused.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or scope is null.</exception>
        public async Task<ScopeProvisionResult> CreateAsync(string tenantId, Scope scope, CancellationToken token = default, bool? filesystemMirror = null)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (string.IsNullOrWhiteSpace(scope.Name)) return ScopeProvisionResult.Refused(400, "BadRequest", "A scope name is required.");

            scope.TenantId = tenantId;
            Scope? conflict = await _Database.Scopes.ReadByNameAsync(tenantId, scope.Name, token).ConfigureAwait(false);
            if (conflict != null) return ScopeProvisionResult.Refused(409, "Conflict", "A scope with that name already exists.");

            if (scope.StoreProvider == StoreProviderEnum.RecallDb)
            {
                // A minimally specified scope must be usable: adopt the tenant's embedding endpoint and its dimension, and
                // refuse with guidance (rather than persist a broken scope) when there is none.
                ModelEndpoint? endpoint;
                if (!string.IsNullOrEmpty(scope.EmbeddingEndpointId))
                {
                    endpoint = await _Database.ModelEndpoints.ReadAsync(tenantId, scope.EmbeddingEndpointId, token).ConfigureAwait(false);
                    if (endpoint == null) return ScopeProvisionResult.Refused(400, "BadRequest", "The specified embeddingEndpointId was not found in this tenant.");
                }
                else
                {
                    endpoint = await FirstActiveEndpointAsync(tenantId, EndpointKindEnum.Embedding, false, token).ConfigureAwait(false);
                }

                if (endpoint == null)
                    return ScopeProvisionResult.Refused(400, "BadRequest", "A RecallDb scope needs an embedding endpoint, but none is configured for this tenant. Create the scope with storeProvider 'Filesystem' for keyword-only memory, or configure an embedding endpoint first (list them with endpoint_enumerate).");

                scope.EmbeddingEndpointId = endpoint.Id;
                if (scope.Dimensionality <= 0) scope.Dimensionality = endpoint.Dimensionality;

                // Reranking is the largest retrieval gain measured, so a new semantic scope uses the tenant's first active
                // cross-encoder (a fast, rerank-only inference endpoint) when one exists. A chat model is never attached
                // automatically, because prompted reranking takes seconds per search. Clear rerankEndpointId to opt out.
                if (string.IsNullOrEmpty(scope.RerankEndpointId))
                {
                    ModelEndpoint? reranker = await FirstActiveEndpointAsync(tenantId, EndpointKindEnum.Inference, true, token).ConfigureAwait(false);
                    if (reranker != null) scope.RerankEndpointId = reranker.Id;
                }

                if (scope.Dimensionality <= 0)
                    return ScopeProvisionResult.Refused(400, "BadRequest", "The embedding endpoint '" + endpoint.Id + "' has no dimensionality configured; pass 'dimensionality' explicitly (e.g. 384 for all-minilm).");
            }

            ApplyMirrorDefault(scope, filesystemMirror);
            string? storageError = ValidateStorage(scope);
            if (storageError != null) return ScopeProvisionResult.Refused(400, "BadRequest", storageError);

            string? endpointError = await ValidateEndpointsAsync(tenantId, scope, token).ConfigureAwait(false);
            if (endpointError != null) return ScopeProvisionResult.Refused(400, "BadRequest", endpointError);

            Scope created = await _Database.Scopes.CreateAsync(scope, token).ConfigureAwait(false);
            return ScopeProvisionResult.Created(created);
        }

        /// <summary>
        /// Check a scope's filesystem settings: the mirror applies only to RecallDb scopes and needs a target path, and
        /// that path must be a directory the server can create and write. Creates the directory when it is missing.
        /// </summary>
        /// <param name="scope">The scope to check.</param>
        /// <returns>An error message, or null when the settings are usable.</returns>
        /// <exception cref="ArgumentNullException">Thrown when scope is null.</exception>
        public string? ValidateStorage(Scope scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (!scope.FilesystemMirror) return null;

            if (scope.StoreProvider != StoreProviderEnum.RecallDb)
                return "filesystemMirror applies only to RecallDb scopes; a Filesystem scope already writes its memories as files.";
            if (string.IsNullOrWhiteSpace(scope.TargetPath))
                return "filesystemMirror needs a targetPath: the directory, on the NotDory server host, that the Open Knowledge Format bundle is written under (in its .okf directory), usually the repository root.";
            if (scope.TargetPath.StartsWith("~", StringComparison.Ordinal))
                return "The targetPath '" + scope.TargetPath + "' starts with '~', which the server does not expand; pass an absolute path.";

            try
            {
                string full = Path.GetFullPath(scope.TargetPath);
                if (File.Exists(full)) return "The targetPath '" + scope.TargetPath + "' is a file; the filesystem mirror needs a directory.";
                Directory.CreateDirectory(full);
                return null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                return "The targetPath '" + scope.TargetPath + "' cannot be used for the filesystem mirror: " + e.Message;
            }
        }

        /// <summary>
        /// Check that each model a scope names is an inference endpoint in the tenant whose API format can do the job: any
        /// reranking-capable format for the reranker, a chat format for the chat and query models.
        /// </summary>
        /// <param name="tenantId">The tenant.</param>
        /// <param name="scope">The scope.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An error message, or null when every named model can do its job.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or scope is null.</exception>
        public async Task<string?> ValidateEndpointsAsync(string tenantId, Scope scope, CancellationToken token = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            return await ValidateEndpointAsync(tenantId, scope.RerankEndpointId, true, "rerankEndpointId", token).ConfigureAwait(false)
                ?? await ValidateEndpointAsync(tenantId, scope.InferenceEndpointId, false, "inferenceEndpointId", token).ConfigureAwait(false)
                ?? await ValidateEndpointAsync(tenantId, scope.QueryEndpointId, false, "queryEndpointId", token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// A new RecallDb scope given a targetPath mirrors to it unless the request said otherwise. NotDory never picks the
        /// directory itself: the bundle belongs in the project (OKF recommends a git repository), which only the caller
        /// knows, so a scope created with no targetPath and no explicit request starts unmirrored.
        /// </summary>
        private void ApplyMirrorDefault(Scope scope, bool? requested)
        {
            if (scope.StoreProvider != StoreProviderEnum.RecallDb) return;
            scope.FilesystemMirror = requested ?? (_Storage.MirrorByDefault && !string.IsNullOrWhiteSpace(scope.TargetPath));
        }

        private async Task<ModelEndpoint?> FirstActiveEndpointAsync(string tenantId, EndpointKindEnum kind, bool rerankOnly, CancellationToken token)
        {
            EnumerationResult<ModelEndpoint> result = await _Database.ModelEndpoints.EnumerateAsync(tenantId, kind, new EnumerationQuery { MaxResults = 1000 }, token).ConfigureAwait(false);
            foreach (ModelEndpoint endpoint in result.Objects)
            {
                if (!endpoint.Active) continue;
                if (rerankOnly && !ApiFormatCapabilities.IsRerankOnly(endpoint.ApiFormat)) continue;
                return endpoint;
            }

            return null;
        }

        private async Task<string?> ValidateEndpointAsync(string tenantId, string? endpointId, bool rerank, string field, CancellationToken token)
        {
            if (string.IsNullOrEmpty(endpointId)) return null;

            ModelEndpoint? endpoint = await _Database.ModelEndpoints.ReadAsync(tenantId, endpointId, token).ConfigureAwait(false);
            if (endpoint == null) return "The specified " + field + " was not found in this tenant.";
            if (endpoint.Kind != EndpointKindEnum.Inference) return "The specified " + field + " is an " + endpoint.Kind + " endpoint; an inference endpoint is required.";
            if (rerank && !ApiFormatCapabilities.CanRerank(endpoint.ApiFormat))
                return "The specified " + field + " uses the " + endpoint.ApiFormat + " format, which cannot rerank; choose a cross-encoder (Tei or Cohere) or a chat model.";
            if (!rerank && !ApiFormatCapabilities.CanChat(endpoint.ApiFormat))
                return "The specified " + field + " is a " + endpoint.ApiFormat + " cross-encoder, which can only rerank; choose a chat model.";
            return null;
        }

        #endregion
    }
}
