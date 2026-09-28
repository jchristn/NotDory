namespace Isis.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Isis.Core.Database;
    using Isis.Core.Enums;
    using Isis.Core.Models;
    using Isis.Core.Recall;
    using Isis.Core.Security;
    using Isis.Core.Stores;
    using Isis.Server.Models;
    using Isis.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using HttpRequestException = System.Net.Http.HttpRequestException;

    /// <summary>
    /// Memory routes, scoped to a tenant and scope. Includes create/upsert, read, delete, list, and search.
    /// </summary>
    public class MemoryRoutes
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly AuthorizationService _Authorization;
        private readonly MemoryService _MemoryService;
        private readonly LookupCache? _Cache;
        private readonly QueryPreparer? _Preparer;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">The database driver.</param>
        /// <param name="authorization">The authorization service.</param>
        /// <param name="memoryService">The memory service.</param>
        /// <param name="cache">Optional lookup cache for scope reads.</param>
        /// <param name="preparer">Optional query preparer that splits and expands searches per the request and the
        /// scope. Null searches every query as given.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public MemoryRoutes(DatabaseDriverBase database, AuthorizationService authorization, MemoryService memoryService, LookupCache? cache = null, QueryPreparer? preparer = null)
        {
            _Cache = cache;
            _Preparer = preparer;
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _MemoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register routes.
        /// </summary>
        /// <param name="server">The webserver.</param>
        public void Register(Webserver server)
        {
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories", ListAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List memories", "Memories"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories", UpsertAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create or update a memory", "Memories"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories/search", SearchAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Search memories", "Memories"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories/{memoryId}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read a memory", "Memories"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories/{memoryId}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete a memory", "Memories"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories/batch-get", BatchGetAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Batch-get memories", "Memories"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories/batch", BatchUpsertAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Batch-upsert memories", "Memories"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories/batch-delete", BatchDeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Batch-delete memories", "Memories"));
        }

        #endregion

        #region Private-Methods

        private async Task<Category?> ResolveUpsertCategoryAsync(string tenantId, string scopeId, string categoryIdOrName, CancellationToken token)
        {
            // An upsert names its category by cat_ id or by name. A name that is new to the scope creates the category, so
            // an agent can write a memory without first looking up or creating one; an unknown cat_ id is still an error.
            Category? category = await _Database.Categories.ReadAsync(tenantId, categoryIdOrName, token).ConfigureAwait(false);
            if (category != null) return category.ScopeId == scopeId ? category : null;

            string name = categoryIdOrName.Trim();
            category = await _Database.Categories.ReadByNameAsync(tenantId, scopeId, name, token).ConfigureAwait(false);
            if (category != null) return category;
            if (name.StartsWith("cat_", StringComparison.Ordinal) || name.Length > 128) return null;

            // Agents vary capitalization ("Decisions", "decisions"); that should not split one category into two.
            EnumerationResult<Category> existing = await _Database.Categories.EnumerateAsync(tenantId, scopeId, new EnumerationQuery { MaxResults = 1000 }, token).ConfigureAwait(false);
            category = existing.Objects.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (category != null) return category;

            return await _Database.Categories.CreateAsync(new Category { TenantId = tenantId, ScopeId = scopeId, Name = name }, token).ConfigureAwait(false);
        }

        private bool Authorize(HttpContextBase context, out string tenantId, out string scopeId)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            tenantId = RouteHelpers.Param(context, "tenantId") ?? string.Empty;
            scopeId = RouteHelpers.Param(context, "scopeId") ?? string.Empty;
            return _Authorization.CanAccessTenant(ctx, tenantId);
        }

        private async Task<Scope?> LoadScopeAsync(HttpContextBase context, string tenantId, string scopeId)
        {
            if (_Cache != null) return await _Cache.GetScopeAsync(tenantId, scopeId, context.Token).ConfigureAwait(false);
            return await _Database.Scopes.ReadAsync(tenantId, scopeId, context.Token).ConfigureAwait(false);
        }

        private async Task ListAsync(HttpContextBase context)
        {
            if (!Authorize(context, out string tenantId, out string scopeId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            string? categoryId = RouteHelpers.Query(context, "category");
            EnumerationResult<Memory> result = await _Database.Memories.EnumerateAsync(tenantId, scopeId, categoryId, RouteHelpers.Enumeration(context), context.Token).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(context, 200, result).ConfigureAwait(false);
        }

        private async Task UpsertAsync(HttpContextBase context)
        {
            if (!Authorize(context, out string tenantId, out string scopeId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            Memory? body = RouteHelpers.Body<Memory>(context);
            if (body == null)
            {
                // Distinguish an unparseable-but-present body from a genuinely missing one, so a caller that
                // sent, say, a bad field type is not misdirected to "slug and categoryId".
                if (!string.IsNullOrWhiteSpace(context.Request.DataAsString))
                {
                    await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "The request body could not be parsed as a memory. Check the JSON and field types (note: 'type' is optional and, if set, should be one of User, Feedback, Project, Reference).").ConfigureAwait(false);
                    return;
                }

                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A memory requires a slug and a categoryId.").ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(body.Slug) || string.IsNullOrWhiteSpace(body.CategoryId))
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A memory requires a slug and a categoryId.").ConfigureAwait(false);
                return;
            }

            Scope? scope = await LoadScopeAsync(context, tenantId, scopeId).ConfigureAwait(false);
            if (scope == null)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Scope not found.").ConfigureAwait(false);
                return;
            }

            Category? category = await ResolveUpsertCategoryAsync(tenantId, scopeId, body.CategoryId, context.Token).ConfigureAwait(false);
            if (category == null)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "The categoryId does not belong to this scope.").ConfigureAwait(false);
                return;
            }

            body.CategoryId = category.Id;

            try
            {
                Memory saved = await _MemoryService.UpsertAsync(scope, category, body, context.Token).ConfigureAwait(false);
                await RouteHelpers.JsonAsync(context, 200, saved).ConfigureAwait(false);
            }
            catch (NotSupportedException ex)
            {
                await RouteHelpers.ErrorAsync(context, 501, "NotImplemented", ex.Message).ConfigureAwait(false);
            }
            catch (ModelEndpointUnavailableException ex)
            {
                await RouteHelpers.ErrorAsync(context, 503, "ServiceUnavailable", ex.Message).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", ex.Message).ConfigureAwait(false);
            }
        }

        private async Task<ModelEndpoint?> ResolveInferenceEndpointAsync(string tenantId, string? endpointId, System.Threading.CancellationToken token)
        {
            if (!string.IsNullOrEmpty(endpointId))
            {
                ModelEndpoint? explicitEndpoint = await _Database.ModelEndpoints.ReadAsync(tenantId, endpointId, token).ConfigureAwait(false);
                return explicitEndpoint != null && explicitEndpoint.Kind == EndpointKindEnum.Inference && explicitEndpoint.Active ? explicitEndpoint : null;
            }

            EnumerationResult<ModelEndpoint> endpoints = await _Database.ModelEndpoints.EnumerateAsync(tenantId, EndpointKindEnum.Inference, new EnumerationQuery { MaxResults = 1000 }, token).ConfigureAwait(false);
            return endpoints.Objects.FirstOrDefault(e => e.Active);
        }

        private async Task SearchAsync(HttpContextBase context)
        {
            if (!Authorize(context, out string tenantId, out string scopeId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            Scope? scope = await LoadScopeAsync(context, tenantId, scopeId).ConfigureAwait(false);
            if (scope == null)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Scope not found.").ConfigureAwait(false);
                return;
            }

            MemorySearchQuery? query = RouteHelpers.Body<MemorySearchQuery>(context);
            if (query == null || string.IsNullOrWhiteSpace(query.QueryText))
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A search requires a non-empty queryText.").ConfigureAwait(false);
                return;
            }

            try
            {
                // Optional query splitting: an inference endpoint rewrites a multi-part question into sub-queries,
                // which are searched alongside it. Without an endpoint the question is searched as given.
                // Optional query steps, per the request and then the scope: split a multi-part question, and expand the
                // query with a drafted answer (searched by vector) and keywords (searched as text), both fused below the
                // query's own weight. Automatic expansion runs only when the search is not reranked.
                string? modelNotice = null;
                if (_Preparer != null)
                {
                    bool reranked = !string.IsNullOrEmpty(scope.RerankEndpointId) && query.Rerank != false;
                    bool decompose = _Preparer.ShouldDecompose(scope, query.Decompose);
                    bool expand = _Preparer.ShouldExpand(scope, query.Expand, reranked);
                    if (decompose || expand)
                    {
                        ModelEndpoint? queryEndpoint = await _Preparer.ResolveQueryEndpointAsync(scope, query.InferenceEndpointId, null, context.Token).ConfigureAwait(false);
                        if (queryEndpoint == null && (query.Decompose == true || query.Expand == true))
                            modelNotice = "No active inference endpoint is available to split or expand the query; it was searched as given.";
                        await _Preparer.PrepareAsync(query, queryEndpoint, null, false, decompose, expand, context.Token).ConfigureAwait(false);
                    }
                }

                MemorySearchResult result = await _MemoryService.SearchAsync(scope, query, context.Token).ConfigureAwait(false);
                if (modelNotice != null) result.Notice = string.IsNullOrEmpty(result.Notice) ? modelNotice : result.Notice + " " + modelNotice;
                List<string> ran = new List<string>(query.AdditionalQueries ?? new List<string>());
                if (query.SubQueries != null) ran.AddRange(query.SubQueries.Select(q => q.Text));
                if (ran.Count > 0) result.Queries = new List<string> { query.QueryText }.Concat(ran).ToList();
                await RouteHelpers.JsonAsync(context, 200, result).ConfigureAwait(false);
            }
            catch (NotSupportedException ex)
            {
                await RouteHelpers.ErrorAsync(context, 501, "NotImplemented", ex.Message).ConfigureAwait(false);
            }
            catch (ModelEndpointUnavailableException ex)
            {
                await RouteHelpers.ErrorAsync(context, 503, "ServiceUnavailable", ex.Message).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", ex.Message).ConfigureAwait(false);
            }
        }

        private async Task ReadAsync(HttpContextBase context)
        {
            if (!Authorize(context, out string tenantId, out string scopeId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            string memoryId = RouteHelpers.Param(context, "memoryId") ?? string.Empty;
            Memory? memory = await _Database.Memories.ReadAsync(tenantId, memoryId, context.Token).ConfigureAwait(false);
            if (memory == null || memory.ScopeId != scopeId)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Memory not found.").ConfigureAwait(false);
                return;
            }

            await RouteHelpers.JsonAsync(context, 200, memory).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase context)
        {
            if (!Authorize(context, out string tenantId, out string scopeId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            string memoryId = RouteHelpers.Param(context, "memoryId") ?? string.Empty;
            Memory? memory = await _Database.Memories.ReadAsync(tenantId, memoryId, context.Token).ConfigureAwait(false);
            if (memory == null || memory.ScopeId != scopeId)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Memory not found.").ConfigureAwait(false);
                return;
            }

            Scope? scope = await LoadScopeAsync(context, tenantId, scopeId).ConfigureAwait(false);
            if (scope == null)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Scope not found.").ConfigureAwait(false);
                return;
            }

            await _MemoryService.DeleteAsync(scope, memory, context.Token).ConfigureAwait(false);
            context.Response.StatusCode = 204;
            await context.Response.Send().ConfigureAwait(false);
        }

        private async Task BatchGetAsync(HttpContextBase context)
        {
            if (!Authorize(context, out string tenantId, out string scopeId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            BatchIdsRequest? request = RouteHelpers.Body<BatchIdsRequest>(context);
            List<Memory> objects = new List<Memory>();
            if (request != null && request.Ids != null && request.Ids.Count > 0)
            {
                objects = await _Database.Memories.ReadManyAsync(tenantId, request.Ids, context.Token).ConfigureAwait(false);
            }

            Dictionary<string, object?> body = new Dictionary<string, object?>();
            body["objects"] = objects;
            await RouteHelpers.JsonAsync(context, 200, body).ConfigureAwait(false);
        }

        private async Task BatchUpsertAsync(HttpContextBase context)
        {
            if (!Authorize(context, out string tenantId, out string scopeId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            BatchMemoryRequest? request = RouteHelpers.Body<BatchMemoryRequest>(context);
            List<Memory> objects = new List<Memory>();
            if (request != null && request.Items != null && request.Items.Count > 0)
            {
                Scope? scope = await LoadScopeAsync(context, tenantId, scopeId).ConfigureAwait(false);
                if (scope == null)
                {
                    await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Scope not found.").ConfigureAwait(false);
                    return;
                }

                try
                {
                    foreach (Memory item in request.Items)
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.Slug) || string.IsNullOrWhiteSpace(item.CategoryId)) continue;

                        Category? category = await _Database.Categories.ReadAsync(tenantId, item.CategoryId, context.Token).ConfigureAwait(false);
                        if (category == null || category.ScopeId != scopeId) continue;

                        Memory saved = await _MemoryService.UpsertAsync(scope, category, item, context.Token).ConfigureAwait(false);
                        objects.Add(saved);
                    }
                }
                catch (NotSupportedException ex)
                {
                    await RouteHelpers.ErrorAsync(context, 501, "NotImplemented", ex.Message).ConfigureAwait(false);
                    return;
                }
                catch (ModelEndpointUnavailableException ex)
                {
                    await RouteHelpers.ErrorAsync(context, 503, "ServiceUnavailable", ex.Message + " (" + objects.Count + " item(s) were saved before the failure).").ConfigureAwait(false);
                    return;
                }
                catch (InvalidOperationException ex)
                {
                    await RouteHelpers.ErrorAsync(context, 400, "BadRequest", ex.Message + " (" + objects.Count + " item(s) were saved before the failure).").ConfigureAwait(false);
                    return;
                }
                catch (HttpRequestException ex)
                {
                    await RouteHelpers.ErrorAsync(context, 503, "ServiceUnavailable", "A backing service could not be reached after " + objects.Count + " item(s) were saved: " + ex.Message).ConfigureAwait(false);
                    return;
                }
            }

            Dictionary<string, object?> body = new Dictionary<string, object?>();
            body["objects"] = objects;
            await RouteHelpers.JsonAsync(context, 201, body).ConfigureAwait(false);
        }

        private async Task BatchDeleteAsync(HttpContextBase context)
        {
            if (!Authorize(context, out string tenantId, out string scopeId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            BatchIdsRequest? request = RouteHelpers.Body<BatchIdsRequest>(context);
            int deleted = 0;
            if (request != null && request.Ids != null && request.Ids.Count > 0)
            {
                Scope? scope = await LoadScopeAsync(context, tenantId, scopeId).ConfigureAwait(false);
                if (scope == null)
                {
                    await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Scope not found.").ConfigureAwait(false);
                    return;
                }

                foreach (string memoryId in request.Ids)
                {
                    Memory? memory = await _Database.Memories.ReadAsync(tenantId, memoryId, context.Token).ConfigureAwait(false);
                    if (memory == null || memory.ScopeId != scopeId) continue;

                    await _MemoryService.DeleteAsync(scope, memory, context.Token).ConfigureAwait(false);
                    deleted++;
                }
            }

            Dictionary<string, object?> body = new Dictionary<string, object?>();
            body["deleted"] = deleted;
            await RouteHelpers.JsonAsync(context, 200, body).ConfigureAwait(false);
        }

        #endregion
    }
}
