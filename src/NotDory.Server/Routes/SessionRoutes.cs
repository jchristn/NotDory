namespace NotDory.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using NotDory.Core.Database;
    using NotDory.Core.Security;
    using NotDory.Server.Models;
    using NotDory.Server.Services;
    using NotDory.Server.Settings;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Session start: the first call an agent makes. The tenant comes from the caller's credential, so no tenant id is
    /// needed.
    /// </summary>
    public class SessionRoutes
    {
        #region Private-Members

        private readonly AuthorizationService _Authorization;
        private readonly SessionStartService _Sessions;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the routes.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authorization">Authorization service.</param>
        /// <param name="settings">The running settings (session start uses the administrator's agent instructions).</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public SessionRoutes(DatabaseDriverBase database, AuthorizationService authorization, NotDorySettings settings)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _Authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _Sessions = new SessionStartService(database, () => settings.Agent.EffectiveServerInstructions());
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register the routes.
        /// </summary>
        /// <param name="server">The webserver.</param>
        /// <exception cref="ArgumentNullException">Thrown when server is null.</exception>
        public void Register(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/session", StartAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Start an agent session: the project's scope, protocol, categories, instructions, and recent memories", "Sessions"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/session", StartAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Start an agent session (query-string form, for hooks)", "Sessions"));
        }

        #endregion

        #region Private-Methods

        private async Task StartAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string tenantId = ctx.TenantId ?? string.Empty;
            if (string.IsNullOrEmpty(tenantId) || !_Authorization.CanAccessTenant(ctx, tenantId))
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "The caller has no tenant.").ConfigureAwait(false);
                return;
            }

            // POST takes a JSON body; GET (and a POST without a body) takes the query string, which is what a shell hook sends.
            SessionStartRequest? request = context.Request.Method == HttpMethod.POST && !string.IsNullOrWhiteSpace(context.Request.DataAsString)
                ? RouteHelpers.Body<SessionStartRequest>(context)
                : null;
            if (request == null)
            {
                request = new SessionStartRequest
                {
                    Project = RouteHelpers.Query(context, "project"),
                    Remote = RouteHelpers.Query(context, "remote"),
                    Directory = RouteHelpers.Query(context, "directory")
                };
                string? create = RouteHelpers.Query(context, "createIfMissing");
                if (create != null) request.CreateIfMissing = !string.Equals(create, "false", StringComparison.OrdinalIgnoreCase);
                int? max = RouteHelpers.QueryInt(context, "maxMemories");
                if (max.HasValue) request.MaxMemories = max.Value;
            }

            SessionStartResult result = await _Sessions.StartAsync(tenantId, ctx.PrincipalName, request, context.Token).ConfigureAwait(false);
            if (string.Equals(RouteHelpers.Query(context, "format"), "text", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/markdown; charset=utf-8";
                await context.Response.Send(SessionStartService.ToMarkdown(result)).ConfigureAwait(false);
                return;
            }

            await RouteHelpers.JsonAsync(context, 200, result).ConfigureAwait(false);
        }

        #endregion
    }
}
