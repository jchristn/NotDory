namespace Isis.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Isis.Core.Database;
    using Isis.Core.Enums;
    using Isis.Core.Models;
    using Isis.Core.Recall;
    using Isis.Core.Stores;

    /// <summary>
    /// Decides, per scope, which model handles each job and which optional query steps run, then applies those steps to
    /// a search before retrieval: rewriting a chat follow-up into a standalone query, splitting a multi-part question,
    /// and expanding the query with a drafted answer and keywords. Search and chat both use it, so a scope behaves the
    /// same way through REST, MCP, and chat.
    /// </summary>
    /// <remarks>
    /// Each setting resolves in the same order: the request, then the scope, then the server default. Models resolve
    /// as request, scope-specific endpoint, the scope's chat model, then the tenant's first active inference endpoint.
    /// </remarks>
    public class QueryPreparer
    {
        #region Public-Members

        /// <summary>
        /// Whether chat follow-ups are rewritten when the scope does not say. Default true.
        /// </summary>
        public bool DefaultConversationRewrite { get; set; } = true;

        /// <summary>
        /// When queries are expanded when the scope does not say. Default <see cref="QueryExpansionModeEnum.Auto"/>.
        /// </summary>
        public QueryExpansionModeEnum DefaultQueryExpansion { get; set; } = QueryExpansionModeEnum.Auto;

        /// <summary>
        /// Whether multi-part questions are split when the scope does not say. Default false.
        /// </summary>
        public bool DefaultQueryDecomposition { get; set; } = false;

        /// <summary>
        /// The follow-up rewriter; its settings bound how much conversation is used.
        /// </summary>
        public ConversationRewriter Rewriter
        {
            get
            {
                return _Rewriter;
            }
        }

        /// <summary>
        /// The query expander.
        /// </summary>
        public QueryExpander Expander
        {
            get
            {
                return _Expander;
            }
        }

        /// <summary>
        /// The query decomposer.
        /// </summary>
        public QueryDecomposer Decomposer
        {
            get
            {
                return _Decomposer;
            }
        }

        #endregion

        #region Private-Members

        private readonly MemoryService _MemoryService;
        private readonly DatabaseDriverBase? _Database;
        private readonly ConversationRewriter _Rewriter;
        private readonly QueryExpander _Expander;
        private readonly QueryDecomposer _Decomposer;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the preparer.
        /// </summary>
        /// <param name="memoryService">The memory service (for the default expansion weight).</param>
        /// <param name="inferenceService">The inference service the query steps call.</param>
        /// <param name="database">The database used to resolve model endpoints by id. Null resolves only endpoints
        /// passed in directly.</param>
        /// <exception cref="ArgumentNullException">Thrown when memoryService or inferenceService is null.</exception>
        public QueryPreparer(MemoryService memoryService, InferenceService inferenceService, DatabaseDriverBase? database = null)
        {
            _MemoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
            if (inferenceService == null) throw new ArgumentNullException(nameof(inferenceService));
            _Database = database;
            _Rewriter = new ConversationRewriter(inferenceService);
            _Expander = new QueryExpander(inferenceService);
            _Decomposer = new QueryDecomposer(inferenceService);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Resolve the model that answers chat for a scope: the requested endpoint, then the scope's
        /// <c>InferenceEndpointId</c>, then the tenant's first active inference endpoint.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="requestedEndpointId">An endpoint the caller named, or null. A named endpoint that is missing,
        /// inactive, or not an inference endpoint resolves to null rather than falling back.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The endpoint, or null when none is available.</returns>
        /// <exception cref="ArgumentNullException">Thrown when scope is null.</exception>
        public async Task<ModelEndpoint?> ResolveChatEndpointAsync(Scope scope, string? requestedEndpointId, CancellationToken token = default)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (!string.IsNullOrEmpty(requestedEndpointId)) return await ReadInferenceAsync(scope.TenantId, requestedEndpointId, token).ConfigureAwait(false);
            ModelEndpoint? scoped = await ReadInferenceAsync(scope.TenantId, scope.InferenceEndpointId, token).ConfigureAwait(false);
            return scoped ?? await FirstActiveInferenceAsync(scope.TenantId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolve the model that rewrites, expands, and splits queries for a scope: the requested endpoint, then the
        /// scope's <c>QueryEndpointId</c>, then its <c>InferenceEndpointId</c>, then the chat model in use, then the
        /// tenant's first active inference endpoint.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="requestedEndpointId">An endpoint the caller named, or null.</param>
        /// <param name="chatEndpoint">The chat model in use, when called from chat; otherwise null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The endpoint, or null when none is available.</returns>
        /// <exception cref="ArgumentNullException">Thrown when scope is null.</exception>
        public async Task<ModelEndpoint?> ResolveQueryEndpointAsync(Scope scope, string? requestedEndpointId, ModelEndpoint? chatEndpoint, CancellationToken token = default)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (!string.IsNullOrEmpty(requestedEndpointId)) return await ReadInferenceAsync(scope.TenantId, requestedEndpointId, token).ConfigureAwait(false);
            ModelEndpoint? query = await ReadInferenceAsync(scope.TenantId, scope.QueryEndpointId, token).ConfigureAwait(false);
            if (query != null) return query;
            ModelEndpoint? scoped = await ReadInferenceAsync(scope.TenantId, scope.InferenceEndpointId, token).ConfigureAwait(false);
            if (scoped != null) return scoped;
            return chatEndpoint ?? await FirstActiveInferenceAsync(scope.TenantId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Whether a search expands its query.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="requested">The request's <c>expand</c>, or null to follow the scope.</param>
        /// <param name="reranked">Whether the search will be reranked.</param>
        /// <returns>True to expand (when a query model is available).</returns>
        /// <exception cref="ArgumentNullException">Thrown when scope is null.</exception>
        public bool ShouldExpand(Scope scope, bool? requested, bool reranked)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (requested.HasValue) return requested.Value;
            QueryExpansionModeEnum mode = scope.QueryExpansion ?? DefaultQueryExpansion;
            return mode == QueryExpansionModeEnum.On || (mode == QueryExpansionModeEnum.Auto && !reranked);
        }

        /// <summary>
        /// Whether a search splits a multi-part question.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <param name="requested">The request's <c>decompose</c>, or null to follow the scope.</param>
        /// <returns>True to split (when a query model is available).</returns>
        /// <exception cref="ArgumentNullException">Thrown when scope is null.</exception>
        public bool ShouldDecompose(Scope scope, bool? requested)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            return requested ?? scope.QueryDecomposition ?? DefaultQueryDecomposition;
        }

        /// <summary>
        /// Whether chat rewrites follow-up questions for a scope.
        /// </summary>
        /// <param name="scope">The scope.</param>
        /// <returns>True to rewrite.</returns>
        /// <exception cref="ArgumentNullException">Thrown when scope is null.</exception>
        public bool ShouldRewrite(Scope scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            return scope.ConversationRewrite ?? DefaultConversationRewrite;
        }

        /// <summary>
        /// Apply the query steps to a search: rewrite a follow-up (when history is given), split a multi-part question,
        /// and expand the query. Each step adds extra queries to <paramref name="query"/>; the original query always
        /// keeps full weight. A step without a model, or whose model fails, adds nothing.
        /// </summary>
        /// <param name="query">The search, changed in place.</param>
        /// <param name="queryEndpoint">The model for the steps, or null when none is available.</param>
        /// <param name="history">Earlier chat messages, or null.</param>
        /// <param name="rewrite">Whether to rewrite a follow-up.</param>
        /// <param name="decompose">Whether to split a multi-part question.</param>
        /// <param name="expand">Whether to expand.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>What was done.</returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null.</exception>
        public async Task<QueryPreparation> PrepareAsync(MemorySearchQuery query, ModelEndpoint? queryEndpoint, List<ChatTurn>? history, bool rewrite, bool decompose, bool expand, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            QueryPreparation preparation = new QueryPreparation();
            if (queryEndpoint == null) return preparation;

            // A follow-up is searched both as sent and as a standalone rewrite: the rewrite finds what the follow-up is
            // about, and the original keeps any exact terms the rewrite might have dropped. The rewrite is the better
            // query for a follow-up, so it keeps full weight.
            if (rewrite && history != null && history.Count > 0)
            {
                string? standalone = await _Rewriter.RewriteAsync(queryEndpoint, history, query.QueryText, token).ConfigureAwait(false);
                if (standalone != null)
                {
                    preparation.StandaloneQuestion = standalone;
                    AddSubQueries(query, new List<MemorySubQuery> { new MemorySubQuery { Text = standalone, Weight = 1.0 } });
                }
            }

            string basis = preparation.StandaloneQuestion ?? query.QueryText;
            if (decompose)
            {
                List<string> parts = await _Decomposer.DecomposeAsync(queryEndpoint, basis, token).ConfigureAwait(false);
                if (parts.Count > 0)
                {
                    List<string> merged = new List<string>(parts);
                    if (query.AdditionalQueries != null) merged.AddRange(query.AdditionalQueries);
                    query.AdditionalQueries = merged.Count > 8 ? merged.GetRange(0, 8) : merged;
                    preparation.Decomposed = true;
                }
            }

            if (expand)
            {
                QueryExpansion expansion = await _Expander.ExpandAsync(queryEndpoint, basis, token).ConfigureAwait(false);
                List<MemorySubQuery> forms = QueryExpander.ToSubQueries(expansion, query.Mode, query.ExpansionWeight ?? _MemoryService.ExpansionWeight);
                if (forms.Count > 0)
                {
                    AddSubQueries(query, forms);
                    preparation.Expanded = true;
                }
            }

            return preparation;
        }

        #endregion

        #region Private-Methods

        private static void AddSubQueries(MemorySearchQuery query, List<MemorySubQuery> added)
        {
            List<MemorySubQuery> merged = new List<MemorySubQuery>(query.SubQueries ?? new List<MemorySubQuery>());
            merged.AddRange(added);
            query.SubQueries = merged.Count > 8 ? merged.GetRange(0, 8) : merged;
        }

        private async Task<ModelEndpoint?> ReadInferenceAsync(string tenantId, string? endpointId, CancellationToken token)
        {
            if (_Database == null || string.IsNullOrEmpty(endpointId)) return null;
            ModelEndpoint? endpoint = await _Database.ModelEndpoints.ReadAsync(tenantId, endpointId, token).ConfigureAwait(false);
            return endpoint != null && endpoint.Active && endpoint.Kind == EndpointKindEnum.Inference ? endpoint : null;
        }

        private async Task<ModelEndpoint?> FirstActiveInferenceAsync(string tenantId, CancellationToken token)
        {
            if (_Database == null) return null;
            EnumerationResult<ModelEndpoint> endpoints = await _Database.ModelEndpoints.EnumerateAsync(tenantId, EndpointKindEnum.Inference, new EnumerationQuery { MaxResults = 1000 }, token).ConfigureAwait(false);
            foreach (ModelEndpoint candidate in endpoints.Objects)
            {
                if (candidate.Active) return candidate;
            }

            return null;
        }

        #endregion
    }
}
