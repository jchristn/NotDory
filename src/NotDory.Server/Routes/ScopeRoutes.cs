namespace NotDory.Server.Routes
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading;
    using System;
    using NotDory.Core.Database;
    using NotDory.Core.Enums;
    using NotDory.Core.Models;
    using NotDory.Core.Recall;
    using NotDory.Core.Security;
    using NotDory.Server.Models;
    using NotDory.Server.Services;
    using WatsonWebserver.Core.OpenApi;
    using WatsonWebserver.Core;
    using WatsonWebserver;

    /// <summary>
    /// Scope routes, scoped to a tenant.
    /// </summary>
    public class ScopeRoutes
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly AuthorizationService _Authorization;
        private readonly MemoryService _MemoryService;
        private readonly ScopeProvisioner _Provisioner;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">The database driver.</param>
        /// <param name="authorization">The authorization service.</param>
        /// <param name="memoryService">The memory service (used for cascade delete of scope content).</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public ScopeRoutes(DatabaseDriverBase database, AuthorizationService authorization, MemoryService memoryService)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Provisioner = new ScopeProvisioner(database);
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
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/tenants/{tenantId}/scopes", ListAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("List scopes", "Scopes"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tenantId}/scopes", CreateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Create a scope", "Scopes"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/tenants/{tenantId}/scopes/{scopeId}", ReadAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read a scope", "Scopes"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1.0/api/tenants/{tenantId}/scopes/{scopeId}", UpdateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Update a scope", "Scopes"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/tenants/{tenantId}/scopes/{scopeId}", DeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Delete a scope", "Scopes"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tenantId}/scopes/batch-get", BatchGetAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Batch-get scopes", "Scopes"));
            server.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1.0/api/tenants/{tenantId}/scopes/batch-delete", BatchDeleteAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Batch-delete scopes", "Scopes"));
        }

        #endregion

        #region Private-Methods

        private bool Authorize(HttpContextBase context, out RequestContext ctx, out string tenantId)
        {
            ctx = RouteHelpers.Context(context);
            tenantId = RouteHelpers.Param(context, "tenantId") ?? string.Empty;
            return _Authorization.CanAccessTenant(ctx, tenantId);
        }

        private async Task ListAsync(HttpContextBase context)
        {
            if (!Authorize(context, out _, out string tenantId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            EnumerationResult<Scope> result = await _Database.Scopes.EnumerateAsync(tenantId, RouteHelpers.Enumeration(context), context.Token).ConfigureAwait(false);
            await RouteHelpers.JsonAsync(context, 200, result).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase context)
        {
            if (!Authorize(context, out _, out string tenantId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            Scope? scope = RouteHelpers.Body<Scope>(context);
            if (scope == null || string.IsNullOrWhiteSpace(scope.Name))
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A scope name is required.").ConfigureAwait(false);
                return;
            }

            ScopeProvisionResult result = await _Provisioner.CreateAsync(tenantId, scope, context.Token).ConfigureAwait(false);
            if (result.Scope == null)
            {
                await RouteHelpers.ErrorAsync(context, result.StatusCode, result.Error ?? "BadRequest", result.Message ?? "The scope could not be created.").ConfigureAwait(false);
                return;
            }

            await RouteHelpers.JsonAsync(context, 201, result.Scope).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase context)
        {
            if (!Authorize(context, out _, out string tenantId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            string scopeId = RouteHelpers.Param(context, "scopeId") ?? string.Empty;
            Scope? scope = await _Database.Scopes.ReadAsync(tenantId, scopeId, context.Token).ConfigureAwait(false);
            if (scope == null)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Scope not found.").ConfigureAwait(false);
                return;
            }

            await RouteHelpers.JsonAsync(context, 200, scope).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase context)
        {
            if (!Authorize(context, out _, out string tenantId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            string scopeId = RouteHelpers.Param(context, "scopeId") ?? string.Empty;
            Scope? existing = await _Database.Scopes.ReadAsync(tenantId, scopeId, context.Token).ConfigureAwait(false);
            if (existing == null)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Scope not found.").ConfigureAwait(false);
                return;
            }

            Scope? update = RouteHelpers.Body<Scope>(context);
            if (update == null)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A scope body is required.").ConfigureAwait(false);
                return;
            }

            update.Id = scopeId;
            update.TenantId = tenantId;
            update.CreatedUtc = existing.CreatedUtc;
            update.StoreProvider = existing.StoreProvider;
            update.Dimensionality = existing.Dimensionality;
            update.RecallCollectionId = existing.RecallCollectionId;
            string? storageError = _Provisioner.ValidateStorage(update);
            if (storageError != null)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", storageError).ConfigureAwait(false);
                return;
            }

            string? rerankError = await _Provisioner.ValidateEndpointsAsync(tenantId, update, context.Token).ConfigureAwait(false);
            if (rerankError != null)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", rerankError).ConfigureAwait(false);
                return;
            }

            Scope saved = await _Database.Scopes.UpdateAsync(update, context.Token).ConfigureAwait(false);

            // A mirror turned on, or pointed at a new directory, starts empty: copy the scope's existing memories into it.
            bool mirrorStarted = saved.FilesystemMirror && saved.StoreProvider == StoreProviderEnum.RecallDb
                && (!existing.FilesystemMirror || !string.Equals(existing.TargetPath, saved.TargetPath, StringComparison.Ordinal));
            if (mirrorStarted) await _MemoryService.SyncFilesystemMirrorAsync(saved, context.Token).ConfigureAwait(false);

            await RouteHelpers.JsonAsync(context, 200, saved).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase context)
        {
            if (!Authorize(context, out _, out string tenantId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            string scopeId = RouteHelpers.Param(context, "scopeId") ?? string.Empty;
            Scope? scope = await _Database.Scopes.ReadAsync(tenantId, scopeId, context.Token).ConfigureAwait(false);
            if (scope == null)
            {
                await RouteHelpers.ErrorAsync(context, 404, "NotFound", "Scope not found.").ConfigureAwait(false);
                return;
            }

            // Cascade: tear down store content and delete all categories + memories, then the scope.
            await _MemoryService.DeleteScopeAsync(scope, context.Token).ConfigureAwait(false);

            context.Response.StatusCode = 204;
            await context.Response.Send().ConfigureAwait(false);
        }

        private async Task BatchGetAsync(HttpContextBase context)
        {
            if (!Authorize(context, out _, out string tenantId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            BatchIdsRequest? request = RouteHelpers.Body<BatchIdsRequest>(context);
            List<Scope> objects = new List<Scope>();
            if (request != null && request.Ids != null && request.Ids.Count > 0)
            {
                objects = await _Database.Scopes.ReadManyAsync(tenantId, request.Ids, context.Token).ConfigureAwait(false);
            }

            Dictionary<string, object?> body = new Dictionary<string, object?>();
            body["objects"] = objects;
            await RouteHelpers.JsonAsync(context, 200, body).ConfigureAwait(false);
        }

        private async Task BatchDeleteAsync(HttpContextBase context)
        {
            if (!Authorize(context, out _, out string tenantId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Not permitted for this tenant.").ConfigureAwait(false);
                return;
            }

            BatchIdsRequest? request = RouteHelpers.Body<BatchIdsRequest>(context);
            int deleted = 0;
            if (request != null && request.Ids != null && request.Ids.Count > 0)
            {
                foreach (string scopeId in request.Ids)
                {
                    Scope? scope = await _Database.Scopes.ReadAsync(tenantId, scopeId, context.Token).ConfigureAwait(false);
                    if (scope == null) continue;

                    // Cascade: tear down store content and delete all categories + memories, then the scope.
                    await _MemoryService.DeleteScopeAsync(scope, context.Token).ConfigureAwait(false);
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
