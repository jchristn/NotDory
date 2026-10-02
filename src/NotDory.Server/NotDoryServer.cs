namespace NotDory.Server
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using NotDory.Core.Database;
    using NotDory.Core.Health;
    using NotDory.Core.Models;
    using NotDory.Core.Recall;
    using NotDory.Core.Security;
    using NotDory.Core.Stores;
    using NotDory.Server.Observability;
    using NotDory.Server.Routes;
    using NotDory.Server.Services;
    using NotDory.Server.Settings;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// The NotDory REST server host. Owns the Watson webserver and wires the authentication hook, OpenAPI,
    /// CORS, and the feature route registrars.
    /// </summary>
    public class NotDoryServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Effective server settings.
        /// </summary>
        public NotDorySettings Settings { get; }

        #endregion

        #region Private-Members

        // The exception a route failed with, handed from the exception handler to post-routing so the request's history
        // row says what failed. Consumed (removed) by the same request, since Watson may reuse a context object.
        private readonly ConditionalWeakTable<HttpContextBase, Exception> _Failures = new ConditionalWeakTable<HttpContextBase, Exception>();

        private readonly DatabaseDriverBase _Database;
        private readonly AuthenticationService _AuthenticationService;
        private readonly AuthorizationService _AuthorizationService;
        private readonly MemoryService _MemoryService;
        private readonly HttpClient _ProbeClient;
        private readonly SocketsHttpHandler _InferenceHandler;
        private readonly HealthCheckService _HealthCheck;
        private readonly InferenceService _InferenceService;
        private readonly MemoryChatService _ChatService;
        private readonly QueryPreparer _QueryPreparer;
        private readonly RetentionService _RetentionService;
        private readonly Webserver _Server;
        private readonly Action<string>? _Log;
        private readonly LookupCache? _LookupCache;
        private readonly StoreOptions? _StoreOptions;
        private readonly string? _SettingsFile;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the server host.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="database">Initialized database driver.</param>
        /// <param name="authenticationService">Authentication service.</param>
        /// <param name="authorizationService">Authorization service.</param>
        /// <param name="memoryService">Memory service.</param>
        /// <param name="log">Optional log callback.</param>
        /// <param name="storeOptions">Optional external store options (RecallDB).</param>
        /// <param name="settingsFile">Optional settings file path, enabling the server settings routes to persist changes.</param>
        /// <param name="lookupCache">Optional lookup cache shared with the authentication and memory services. Null disables caching in the routes.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public NotDoryServer(
            NotDorySettings settings,
            DatabaseDriverBase database,
            AuthenticationService authenticationService,
            AuthorizationService authorizationService,
            MemoryService memoryService,
            Action<string>? log = null,
            StoreOptions? storeOptions = null,
            string? settingsFile = null,
            LookupCache? lookupCache = null)
        {
            _LookupCache = lookupCache;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _AuthenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
            _AuthorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _MemoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
            _Log = log;
            _StoreOptions = storeOptions;
            _SettingsFile = settingsFile;

            _ProbeClient = new HttpClient();
            _HealthCheck = new HealthCheckService(_ProbeClient);
            // Shared transport for inference; the service wraps it per-endpoint for auth and sets an infinite
            // per-client timeout (inference streams can run far longer than the default 100s), relying on the
            // per-request cancellation token instead.
            _InferenceHandler = new SocketsHttpHandler();
            _InferenceService = new InferenceService(new TransientRetryHandler(_InferenceHandler));
            _QueryPreparer = new QueryPreparer(_MemoryService, _InferenceService, _Database);
            _QueryPreparer.DefaultConversationRewrite = settings.Retrieval.ChatConversationRewrite;
            _QueryPreparer.DefaultQueryExpansion = settings.Retrieval.QueryExpansion;
            _QueryPreparer.DefaultQueryDecomposition = settings.Retrieval.QueryDecomposition;
            _ChatService = new MemoryChatService(_MemoryService, _InferenceService, _QueryPreparer);
            _ChatService.LinkExpansion = settings.Retrieval.ChatLinkExpansion;
            _ChatService.Rewriter.MaxTurns = settings.Retrieval.ChatHistoryTurns;
            _RetentionService = new RetentionService(_Database, Settings.Retention, _Log);

            WebserverSettings webserverSettings = new WebserverSettings();
            RouteHelpers.MaxBodyBytes = settings.Rest.MaxRequestBodyBytes;
            webserverSettings.Hostname = settings.Rest.Hostname;
            webserverSettings.Port = settings.Rest.Port;
            webserverSettings.Ssl.Enable = settings.Rest.Ssl;

            // Enable Watson's built-in OpenTelemetry instrumentation (metrics + traces). The signals are
            // emitted into Watson's default meter/activity source ("Watson"); the in-process ObservabilityHost
            // subscribes to them by name and exports metrics via Prometheus and traces via OTLP. Watson's own
            // Prometheus endpoint stays off — the OTel MeterProvider owns the scrape endpoint on port 9464.
            webserverSettings.Telemetry.Enable = true;
            webserverSettings.Telemetry.EnableMetrics = true;
            webserverSettings.Telemetry.EnableTraces = true;
            webserverSettings.Telemetry.PropagateContext = true;
            webserverSettings.Telemetry.Prometheus.Enable = false;

            _Server = new Webserver(webserverSettings, DefaultRouteAsync);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Configure the pipeline and routes, then start listening.
        /// </summary>
        public void Start()
        {
            ConfigureServer();
            ConfigureRoutes();
            _Server.Start();
            _RetentionService.Start();
        }

        /// <summary>
        /// Stop listening.
        /// </summary>
        public void Stop()
        {
            _RetentionService.Stop();
            if (_Server.IsListening) _Server.Stop();
        }

        /// <summary>
        /// Dispose the server host.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        private void ConfigureServer()
        {
            _Server.Routes.AuthenticateRequest = AuthenticateAsync;
            _Server.Routes.Preflight = PreflightRouteAsync;
            _Server.Routes.PostRouting = PostRoutingRouteAsync;
            _Server.Routes.Exception = ExceptionRouteAsync;

            _Server.UseOpenApi(openApi =>
            {
                openApi.Info.Title = "NotDory API";
                openApi.Info.Version = "v1.0";
                openApi.Info.Description = "NotDory agent memory platform.";
            });
        }

        private void ConfigureRoutes()
        {
            new HealthRoutes(_Database, Settings.NodeId).Register(_Server);
            new ServerInfoRoutes(Settings.NodeId).Register(_Server);
            TenantLifecycleService tenantLifecycle = new TenantLifecycleService(_Database, _MemoryService);
            new AuthRoutes(_Database, Settings.Auth).Register(_Server);
            new TenantRoutes(_Database, _AuthorizationService, tenantLifecycle).Register(_Server);
            new UserRoutes(_Database, _AuthorizationService).Register(_Server);
            new CredentialRoutes(_Database, _AuthorizationService).Register(_Server);
            new ScopeRoutes(_Database, _AuthorizationService, _MemoryService, Settings.Storage).Register(_Server);
            new CategoryRoutes(_Database, _AuthorizationService, _MemoryService).Register(_Server);
            new MemoryRoutes(_Database, _AuthorizationService, _MemoryService, _LookupCache, _QueryPreparer).Register(_Server);
            new ModelEndpointRoutes(_Database, _AuthorizationService, _HealthCheck).Register(_Server);
            new ChatRoutes(_Database, _AuthorizationService, _ChatService, _LookupCache).Register(_Server);
            new RequestHistoryRoutes(_Database, _AuthorizationService).Register(_Server);
            new OperationRoutes(_Database, _AuthorizationService).Register(_Server);
            new CollectionRoutes(_AuthorizationService, _StoreOptions).Register(_Server);
            new GuideRoutes(_Database, _AuthorizationService).Register(_Server);
            new SessionRoutes(_Database, _AuthorizationService, Settings).Register(_Server);
            new AgentProtocolRoutes(Settings, _SettingsFile ?? "notdory.json").Register(_Server);
            new InstructionRoutes(_Database, _AuthorizationService).Register(_Server);
            new SettingsRoutes(Settings, _SettingsFile ?? "notdory.json", _AuthorizationService).Register(_Server);
        }

        private static async Task DefaultRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 404;
            context.Response.ContentType = "application/json";
            await context.Response.Send("{\"error\":\"NotFound\",\"message\":\"No matching route.\"}").ConfigureAwait(false);
        }

        private static async Task PreflightRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, HEAD");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, x-access-key, x-secret-key, x-token");
            context.Response.Headers.Add("Access-Control-Max-Age", "86400");
            await context.Response.Send().ConfigureAwait(false);
        }

        /// <summary>
        /// Last-resort handler for any exception a route did not handle. Without it Watson answers with its own HTML
        /// error page, which API clients (and MCP tool proxies) cannot parse. Maps well-known exception types to the
        /// status codes routes use for them, and everything else to a JSON 500 (details are logged, not returned).
        /// </summary>
        private async Task ExceptionRouteAsync(HttpContextBase context, Exception e)
        {
            ErrorClassification classified = ErrorClassifier.Classify(e, context.Token.IsCancellationRequested);

            _Log?.Invoke("unhandled " + e.GetType().Name + " on " + context.Request.Method + " " + context.Request.Url.RawWithQuery + ": " + e);
            if (context.Response.ResponseSent) return;

            try
            {
                await RouteHelpers.ErrorAsync(context, classified.StatusCode, classified.Error, classified.Message).ConfigureAwait(false);
            }
            catch (Exception sendFailure)
            {
                // The connection is gone or the response was partially written; nothing more can be sent.
                _Log?.Invoke("could not send error response: " + sendFailure.Message);
            }

            // Post-routing records the request; hand it the exception so the history row says what failed. The summary is
            // stored only in the administrator-visible history, never returned to the caller.
            _Failures.AddOrUpdate(context, e);
        }

        /// <summary>
        /// Authentication hook. An exception thrown from the hook skips post-routing, so a request that failed while
        /// authenticating (for example on a database error during the credential lookup) left no trace in request history.
        /// This wrapper catches the failure, answers it with the same status mapping as the exception handler, and hands the
        /// exception to post-routing, which then records the request with its exception summary like any other.
        /// </summary>
        private async Task AuthenticateAsync(HttpContextBase context)
        {
            try
            {
                await _AuthenticationService.AuthenticateRequestAsync(context).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _Failures.AddOrUpdate(context, e);
                ErrorClassification classified = ErrorClassifier.Classify(e, context.Token.IsCancellationRequested);
                _Log?.Invoke("authentication failed with " + e.GetType().Name + " on " + context.Request.Method + " " + context.Request.Url.RawWithQuery + ": " + e);
                if (!context.Response.ResponseSent)
                {
                    try
                    {
                        await RouteHelpers.ErrorAsync(context, classified.StatusCode, classified.Error, classified.Message).ConfigureAwait(false);
                    }
                    catch (Exception sendFailure)
                    {
                        _Log?.Invoke("could not send error response: " + sendFailure.Message);
                    }
                }
            }
        }

        private async Task PostRoutingRouteAsync(HttpContextBase context)
        {
            context.Timestamp.End = DateTime.UtcNow;
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, HEAD");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, x-access-key, x-secret-key, x-token");

            if (_Log != null)
            {
                _Log(context.Request.Method + " " + context.Request.Url.RawWithQuery + " " + context.Response.StatusCode);
            }

            // Drop cached lookups a successful write may have changed (credentials, users, scopes, endpoints). Done
            // centrally so writes proxied through the MCP server are covered too.
            if (_LookupCache != null && context.Response.StatusCode < 400)
            {
                _LookupCache.InvalidateForWrite(context.Request.Method.ToString(), context.Request.Url.RawWithoutQuery ?? string.Empty);
            }

            await CaptureRequestAsync(context).ConfigureAwait(false);
        }

        private async Task CaptureRequestAsync(HttpContextBase context, Exception? exception = null)
        {
            if (exception == null && _Failures.TryGetValue(context, out Exception? routeFailure))
            {
                exception = routeFailure;
                _Failures.Remove(context);
            }

            if (!Settings.RequestHistory.Enabled)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return;
            }

            string path = context.Request.Url.RawWithoutQuery ?? string.Empty;
            if (path.Contains("/api/health", StringComparison.Ordinal) || path.Contains("/metrics", StringComparison.Ordinal))
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return;
            }

            try
            {
                RequestContext? ctx = context.Metadata as RequestContext;
                RequestHistoryEntry entry = new RequestHistoryEntry();
                entry.Method = context.Request.Method.ToString();
                entry.Path = context.Request.Url.RawWithQuery ?? path;
                entry.StatusCode = context.Response.StatusCode;
                entry.DurationMs = context.Timestamp.TotalMs ?? 0.0;
                entry.TenantId = ctx?.TenantId;
                entry.PrincipalName = ctx?.PrincipalName;
                entry.SourceIp = context.Request.Source?.IpAddress;

                if (Settings.RequestHistory.CaptureHeaders)
                {
                    entry.RequestHeaders = BuildHeadersJson(context.Request.Headers);
                    entry.ResponseHeaders = BuildHeadersJson(context.Response.Headers);
                }

                if (exception != null) entry.ResponseHeaders = WithException(entry.ResponseHeaders, exception);

                if (Settings.RequestHistory.CaptureBodies)
                {
                    int maxBytes = Settings.RequestHistory.MaxBodyBytes > 0 ? Settings.RequestHistory.MaxBodyBytes : 16384;
                    entry.RequestBody = CaptureBody(SafeRequestBody(context), maxBytes);
                    entry.ResponseBody = CaptureBody(RouteHelpers.TakeCapturedResponseBody(context), maxBytes);
                }

                await _Database.RequestHistory.CreateAsync(entry, CancellationToken.None).ConfigureAwait(false);

                // Derive a semantic operation event from the same request, so scope/memory/agentic activity
                // can be charted over time. Kept in a separate, identically-pruned table (operation_events).
                if (OperationClassifier.TryClassify(entry.Method, path, out string resourceType, out string operation, out string? resourceId, out string? scopeId))
                {
                    OperationEvent operationEvent = new OperationEvent();
                    operationEvent.TenantId = entry.TenantId;
                    operationEvent.ResourceType = resourceType;
                    operationEvent.Operation = operation;
                    operationEvent.ResourceId = resourceId;
                    operationEvent.ScopeId = scopeId;
                    operationEvent.Method = entry.Method;
                    operationEvent.Path = entry.Path;
                    operationEvent.StatusCode = entry.StatusCode;
                    operationEvent.PrincipalName = entry.PrincipalName;
                    operationEvent.SourceIp = entry.SourceIp;
                    operationEvent.DurationMs = entry.DurationMs;
                    await _Database.OperationEvents.CreateAsync(operationEvent, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch
            {
                // Best-effort capture; never fail the request because of history.
            }
        }

        private static string? SafeRequestBody(HttpContextBase context)
        {
            try
            {
                return context.Request.DataAsString;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Add an exception summary (type and message, then each inner exception) to a captured response-headers JSON
        /// object under <c>x-notdory-exception</c>, so a failed request in history says what failed. Capped at 2,000
        /// characters.
        /// </summary>
        private static string WithException(string? headersJson, Exception exception)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(headersJson))
            {
                try
                {
                    Dictionary<string, string>? existing = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
                    if (existing != null) foreach (KeyValuePair<string, string> pair in existing) map[pair.Key] = pair.Value;
                }
                catch (JsonException)
                {
                }
            }

            List<string> parts = new List<string>();
            for (Exception? current = exception; current != null && parts.Count < 4; current = current.InnerException)
            {
                parts.Add(current.GetType().FullName + ": " + current.Message);
            }

            string summary = string.Join(" --> ", parts);
            map["x-notdory-exception"] = summary.Length > 2000 ? summary.Substring(0, 2000) : summary;
            return JsonSerializer.Serialize(map);
        }

        private static string? BuildHeadersJson(NameValueCollection? headers)
        {
            if (headers == null) return null;

            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string? key in headers.AllKeys)
            {
                if (string.IsNullOrEmpty(key)) continue;
                map[key] = IsSensitiveHeader(key) ? "***" : (headers[key] ?? string.Empty);
            }

            if (map.Count == 0) return null;
            return JsonSerializer.Serialize(map);
        }

        private static bool IsSensitiveHeader(string key)
        {
            string lower = key.ToLowerInvariant();
            return lower == "authorization" || lower == "x-secret-key" || lower == "x-token" || lower == "cookie" || lower == "set-cookie";
        }

        private static string? CaptureBody(string? body, int maxBytes)
        {
            if (string.IsNullOrEmpty(body)) return null;

            string redacted = Regex.Replace(
                body,
                "(\"(?:password|secretKey|secret_key|secret)\"\\s*:\\s*)\"[^\"]*\"",
                "$1\"***\"",
                RegexOptions.IgnoreCase);

            if (redacted.Length > maxBytes) redacted = redacted.Substring(0, maxBytes) + "…[truncated]";
            return redacted;
        }

        private void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                _RetentionService.Dispose();
                if (_Server is IDisposable disposableServer) disposableServer.Dispose();
                _ProbeClient.Dispose();
                _InferenceHandler.Dispose();
            }

            _Disposed = true;
        }

        #endregion
    }
}
