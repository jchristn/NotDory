namespace Isis.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Isis.Core.Helpers;
    using Isis.Core.Security;
    using Isis.Server.Models;
    using Isis.Server.Services;
    using Isis.Server.Settings;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// The agent protocol: everything Isis sends a model when an agent connects (the MCP server instructions and every tool
    /// description). Anyone may read it, since every MCP client sees it anyway, and the MCP server polls it to pick up
    /// edits without a restart; only a system administrator may change it. Changes are saved to the settings file.
    /// </summary>
    public class AgentProtocolRoutes
    {
        #region Private-Members

        private readonly IsisSettings _Settings;
        private readonly string _SettingsFile;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the routes.
        /// </summary>
        /// <param name="settings">The running settings, updated in place.</param>
        /// <param name="settingsFile">The settings file changes are saved to.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public AgentProtocolRoutes(IsisSettings settings, string settingsFile)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _SettingsFile = settingsFile ?? throw new ArgumentNullException(nameof(settingsFile));
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
            server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/agent-protocol", GetAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Read what agents are told on connect: server instructions and tool descriptions, with defaults", "Agent protocol"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.PUT, "/v1.0/api/agent-protocol", UpdateAsync, null,
                openApiMetadata: OpenApiRouteMetadata.Create("Replace the server instructions and tool description overrides (system administrator)", "Agent protocol"));
        }

        /// <summary>
        /// The agent protocol as the API returns it.
        /// </summary>
        /// <param name="agent">The agent settings.</param>
        /// <returns>The response body.</returns>
        /// <exception cref="ArgumentNullException">Thrown when agent is null.</exception>
        public static Dictionary<string, object?> Describe(AgentSettings agent)
        {
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            List<Dictionary<string, object?>> tools = new List<Dictionary<string, object?>>();
            foreach (KeyValuePair<string, string> entry in AgentToolCatalog.Defaults)
            {
                tools.Add(new Dictionary<string, object?>
                {
                    ["name"] = entry.Key,
                    ["description"] = agent.EffectiveToolDescription(entry.Key),
                    ["defaultDescription"] = entry.Value,
                    ["overridden"] = agent.ToolDescriptions.ContainsKey(entry.Key)
                });
            }

            return new Dictionary<string, object?>
            {
                ["serverInstructions"] = agent.EffectiveServerInstructions(),
                ["defaultServerInstructions"] = AgentProtocol.ServerInstructions,
                ["serverInstructionsOverridden"] = agent.ServerInstructions != null,
                ["tools"] = tools
            };
        }

        #endregion

        #region Private-Methods

        private async Task GetAsync(HttpContextBase context)
        {
            await RouteHelpers.JsonAsync(context, 200, Describe(_Settings.Agent)).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            if (!ctx.IsAdmin)
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Editing the agent protocol requires a system administrator.").ConfigureAwait(false);
                return;
            }

            AgentProtocolUpdateRequest? request = RouteHelpers.Body<AgentProtocolUpdateRequest>(context);
            if (request == null)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A body with serverInstructions and/or toolDescriptions is required.").ConfigureAwait(false);
                return;
            }

            Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string?> entry in request.ToolDescriptions ?? new Dictionary<string, string?>())
            {
                if (AgentToolCatalog.DefaultDescription(entry.Key) == null)
                {
                    await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "'" + entry.Key + "' is not an Isis tool; GET /v1.0/api/agent-protocol lists them.").ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(entry.Value)) overrides[entry.Key] = entry.Value;
            }

            AgentSettings updated = new AgentSettings { ServerInstructions = request.ServerInstructions, ToolDescriptions = overrides };
            IsisSettings saved = IsisSettings.FromFile(_SettingsFile);
            saved.Agent = updated;
            try
            {
                saved.ToFile(_SettingsFile);
            }
            catch (Exception e)
            {
                await RouteHelpers.ErrorAsync(context, 500, "WriteFailed", "Could not write the settings file: " + e.Message).ConfigureAwait(false);
                return;
            }

            _Settings.Agent = updated;
            await RouteHelpers.JsonAsync(context, 200, Describe(updated)).ConfigureAwait(false);
        }

        #endregion
    }
}
