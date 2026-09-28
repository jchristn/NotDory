namespace Isis.McpServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Isis.Core;
    using Isis.Core.Helpers;
    using Isis.McpServer.Settings;
    using Voltaic.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// The Isis MCP server. Authenticates the caller from the MCP transport headers and proxies each tool
    /// call to the Isis REST API over loopback, forwarding the caller's credentials so the REST server
    /// performs the authoritative authentication and tenant scoping.
    /// </summary>
    public class IsisMcpServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Effective settings.
        /// </summary>
        public McpServerSettings Settings { get; }

        #endregion

        #region Private-Members

        private readonly AsyncLocal<McpCallerCredentials?> _Caller = new AsyncLocal<McpCallerCredentials?>();
        private readonly HttpClient _RestClient;
        private readonly List<McpToolRegistration> _Tools = new List<McpToolRegistration>();
        private readonly object _ToolLock = new object();
        private Dictionary<string, string> _Descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
        private Task? _RefreshTask = null;
        private readonly ConcurrentDictionary<string, string> _TenantByAccessKey = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private readonly McpHttpServer _Server;
        private CancellationTokenSource? _Cts;
        private Task? _ServerTask;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the MCP server.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public IsisMcpServer(McpServerSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _RestClient = new HttpClient();
            _RestClient.BaseAddress = new Uri(settings.RestBaseUrl());

            // Voltaic 2.x always registers the MCP protocol methods (including ping); the diagnostic echo/getTime tools stay
            // off so tools/list carries only the Isis tools.
            _Server = new McpHttpServer(settings.Hostname, settings.Port, settings.RpcPath, settings.EventsPath, includeDiagnosticTools: false, mcpPath: settings.McpPath);
            _Server.ServerName = "Isis.McpServer";
            _Server.ServerVersion = Constants.ProductVersion;

            // The instructions travel in the initialize result, which agent harnesses place in the model's system prompt:
            // the one channel that reaches the model on connect even when the harness defers tool descriptions. The built-in
            // text applies until the administrator's version is read from the Isis server (RefreshAgentProtocolAsync).
            _Server.ServerInstructions = AgentProtocol.ServerInstructions;
            _Server.EnableCors = true;
            _Server.AuthenticationHandler = AuthenticateAsync;

            RegisterTools();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the MCP server.
        /// </summary>
        /// <param name="token">Cancellation token linked to the server lifetime.</param>
        public void Start(CancellationToken token = default)
        {
            _Cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _ServerTask = _Server.StartAsync(_Cts.Token);
            _RefreshTask = RefreshLoopAsync(_Cts.Token);
        }

        /// <summary>
        /// Read the administrator-editable agent protocol (server instructions and tool description overrides) from the
        /// Isis server and apply it: new connections get the new instructions, and when a tool description changed every
        /// tool is re-registered in order and connected clients are sent tools/list_changed. Runs on start and then every
        /// <see cref="McpServerSettings.AgentProtocolRefreshSeconds"/>; exposed for testing.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the tool descriptions changed.</returns>
        public async Task<bool> RefreshAgentProtocolAsync(CancellationToken token = default)
        {
            using HttpResponseMessage response = await _RestClient.GetAsync("/v1.0/api/agent-protocol", token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;
            string text = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;

            string? instructions = root.TryGetProperty("serverInstructions", out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            _Server.ServerInstructions = string.IsNullOrWhiteSpace(instructions) ? AgentProtocol.ServerInstructions : instructions;

            Dictionary<string, string> descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("tools", out JsonElement tools) && tools.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement tool in tools.EnumerateArray())
                {
                    string? name = tool.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                    string? description = tool.TryGetProperty("description", out JsonElement d) ? d.GetString() : null;
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrWhiteSpace(description)) descriptions[name] = description;
                }
            }

            lock (_ToolLock)
            {
                bool changed = false;
                foreach (McpToolRegistration tool in _Tools)
                {
                    if (!string.Equals(DescriptionFrom(descriptions, tool.Name), DescriptionFrom(_Descriptions, tool.Name), StringComparison.Ordinal)) changed = true;
                }

                if (!changed) return false;
                _Descriptions = descriptions;

                // Re-register every tool, not just the edited ones, so tools/list keeps its order (session_start first).
                foreach (McpToolRegistration tool in _Tools) _Server.UnregisterTool(tool.Name);
                foreach (McpToolRegistration tool in _Tools) _Server.RegisterTool(tool.Name, Describe(tool.Name), tool.InputSchema, tool.Handler);
            }

            _Server.NotifyToolsChanged();
            return true;
        }

        /// <summary>
        /// Stop the MCP server.
        /// </summary>
        public void Stop()
        {
            try
            {
                _Cts?.Cancel();
                _Server.Stop();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Proxy a request to the Isis REST API and return a structured envelope. Exposed for testing.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">The REST path (beginning with a slash).</param>
        /// <param name="jsonBody">The JSON request body, or null.</param>
        /// <param name="tool">The tool name, echoed in the envelope.</param>
        /// <param name="credentials">The caller credentials to forward.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An envelope with success, statusCode, tool, and data.</returns>
        public async Task<object> ProxyAsync(HttpMethod method, string path, string? jsonBody, string tool, McpCallerCredentials credentials, CancellationToken token = default)
        {
            if (credentials == null) throw new ArgumentNullException(nameof(credentials));

            using HttpRequestMessage request = new HttpRequestMessage(method, path);
            if (!string.IsNullOrEmpty(credentials.AccessKey)) request.Headers.Add("x-access-key", credentials.AccessKey);
            if (!string.IsNullOrEmpty(credentials.SecretKey)) request.Headers.Add("x-secret-key", credentials.SecretKey);
            if (jsonBody != null) request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _RestClient.SendAsync(request, token).ConfigureAwait(false);
            string text = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

            Dictionary<string, object?> envelope = new Dictionary<string, object?>();
            envelope["tool"] = tool;
            envelope["success"] = response.IsSuccessStatusCode;
            envelope["statusCode"] = (int)response.StatusCode;
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    envelope["data"] = JsonSerializer.Deserialize<JsonElement>(text);
                }
                catch (JsonException)
                {
                    envelope["data"] = text;
                }
            }

            return envelope;
        }

        /// <summary>
        /// Dispose the server.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            try { _Server.Dispose(); } catch { }
            try { _Cts?.Dispose(); } catch { }
            _RestClient.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        private Task<AuthenticationResult> AuthenticateAsync(HttpListenerRequest request)
        {
            McpCallerCredentials credentials = new McpCallerCredentials();
            credentials.AccessKey = ReadAccessKey(request);
            credentials.SecretKey = request.Headers["x-secret-key"];

            if (!credentials.HasAny())
            {
                AuthenticationResult failure = new AuthenticationResult();
                failure.IsAuthenticated = false;
                failure.StatusCode = 401;
                failure.ErrorMessage = "Provide the tenant credential access key as an 'Authorization: Bearer <accessKey>' token (or the 'x-access-key' header).";
                return Task.FromResult(failure);
            }

            _Caller.Value = credentials;

            AuthenticationResult success = new AuthenticationResult();
            success.IsAuthenticated = true;
            success.Principal = "credential";
            return Task.FromResult(success);
        }

        /// <summary>
        /// Resolve the caller's access key from either an <c>Authorization: Bearer &lt;accessKey&gt;</c> token
        /// (the single-credential form supported by MCP clients such as Mux, which cannot send two headers) or
        /// the <c>x-access-key</c> header. The access key is the public, transferable material; the secret is
        /// never sent as a bearer token and stays client-side.
        /// </summary>
        private static string? ReadAccessKey(HttpListenerRequest request)
        {
            string? authorization = request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                string token = authorization.Substring("Bearer ".Length).Trim();
                if (!string.IsNullOrEmpty(token)) return token;
            }

            return request.Headers["x-access-key"];
        }

        private async Task<string> TenantAsync(RpcParameters? parameters, CancellationToken token)
        {
            // An explicit tenantId wins; otherwise the credential's own tenant (from whoami), cached per access key since a
            // credential belongs to one tenant for its lifetime.
            string? explicitTenant = parameters?.GetString("tenantId");
            if (!string.IsNullOrWhiteSpace(explicitTenant)) return explicitTenant;

            McpCallerCredentials credentials = CurrentCredentials();
            string cacheKey = credentials.AccessKey ?? string.Empty;
            if (cacheKey.Length > 0 && _TenantByAccessKey.TryGetValue(cacheKey, out string? cached)) return cached;

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/v1.0/api/whoami");
            if (!string.IsNullOrEmpty(credentials.AccessKey)) request.Headers.Add("x-access-key", credentials.AccessKey);
            if (!string.IsNullOrEmpty(credentials.SecretKey)) request.Headers.Add("x-secret-key", credentials.SecretKey);
            using HttpResponseMessage response = await _RestClient.SendAsync(request, token).ConfigureAwait(false);
            string text = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            string? tenantId = null;
            if (response.IsSuccessStatusCode)
            {
                using JsonDocument document = JsonDocument.Parse(text);
                if (document.RootElement.TryGetProperty("tenantId", out JsonElement value) && value.ValueKind == JsonValueKind.String) tenantId = value.GetString();
            }

            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentException("Could not resolve your tenant from the credential (whoami returned " + (int)response.StatusCode + "); pass tenantId explicitly.");
            if (cacheKey.Length > 0) _TenantByAccessKey[cacheKey] = tenantId;
            return tenantId;
        }

        private void AddTool(string name, string description, object inputSchema, Func<RpcParameters?, CancellationToken, Task<object>> handler)
        {
            lock (_ToolLock)
            {
                _Tools.Add(new McpToolRegistration(name, inputSchema, handler));
                _Server.RegisterTool(name, description, inputSchema, handler);
            }
        }

        private string Describe(string name)
        {
            return DescriptionFrom(_Descriptions, name);
        }

        private static string DescriptionFrom(Dictionary<string, string> overrides, string name)
        {
            if (overrides.TryGetValue(name, out string? description)) return description;
            return AgentToolCatalog.DefaultDescription(name) ?? throw new InvalidOperationException("The tool '" + name + "' has no default description in AgentToolCatalog.");
        }

        private async Task RefreshLoopAsync(CancellationToken token)
        {
            int seconds = Math.Max(5, Settings.AgentProtocolRefreshSeconds);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await RefreshAgentProtocolAsync(token).ConfigureAwait(false);
                }
                catch (Exception e) when (!(e is OperationCanceledException && token.IsCancellationRequested))
                {
                    // The Isis server may still be starting or be briefly unreachable; the current text stays in effect.
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(seconds), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private McpCallerCredentials CurrentCredentials()
        {
            return _Caller.Value ?? new McpCallerCredentials();
        }

        private static string Require(RpcParameters? parameters, string name)
        {
            string? value = parameters?.GetString(name);
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("Argument '" + name + "' is required.");
            return value;
        }

        private static string Encode(string value)
        {
            return Uri.EscapeDataString(value);
        }

        private static List<Dictionary<string, object>>? SubQueries(RpcParameters? parameters)
        {
            // Accept a JSON array of { text, weight?, mode? } objects; entries without text are skipped.
            string? raw = parameters?.RawJson;
            if (string.IsNullOrEmpty(raw)) return null;

            using JsonDocument document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("subQueries", out JsonElement value)) return null;
            if (value.ValueKind != JsonValueKind.Array) return null;

            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                string text = item.TryGetProperty("text", out JsonElement t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? string.Empty) : string.Empty;
                if (string.IsNullOrWhiteSpace(text)) continue;
                Dictionary<string, object> entry = new Dictionary<string, object> { { "text", text } };
                if (item.TryGetProperty("weight", out JsonElement w) && w.ValueKind == JsonValueKind.Number) entry["weight"] = w.GetDouble();
                if (item.TryGetProperty("mode", out JsonElement m) && m.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(m.GetString())) entry["mode"] = m.GetString()!;
                result.Add(entry);
            }

            return result;
        }

        private static List<Dictionary<string, string>>? ChatHistory(RpcParameters? parameters)
        {
            // Accept a JSON array of { role, content } objects; entries without content are skipped.
            string? raw = parameters?.RawJson;
            if (string.IsNullOrEmpty(raw)) return null;

            using JsonDocument document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("history", out JsonElement value)) return null;
            if (value.ValueKind != JsonValueKind.Array) return null;

            List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                string content = item.TryGetProperty("content", out JsonElement c) && c.ValueKind == JsonValueKind.String ? (c.GetString() ?? string.Empty) : string.Empty;
                if (string.IsNullOrWhiteSpace(content)) continue;
                string role = item.TryGetProperty("role", out JsonElement r) && r.ValueKind == JsonValueKind.String ? (r.GetString() ?? "user") : "user";
                result.Add(new Dictionary<string, string> { { "role", role }, { "content", content } });
            }

            return result;
        }

        private static List<string>? StringList(RpcParameters? parameters, string name)
        {
            // Accept a JSON array of strings, or a single comma-separated string from clients that flatten arrays.
            string? raw = parameters?.RawJson;
            if (string.IsNullOrEmpty(raw)) return null;

            using JsonDocument document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty(name, out JsonElement value)) return null;

            List<string> result = new List<string>();
            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())) result.Add(item.GetString()!.Trim());
                }
            }
            else if (value.ValueKind == JsonValueKind.String)
            {
                foreach (string part in (value.GetString() ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) result.Add(part);
            }
            else
            {
                return null;
            }

            return result;
        }

        private static void AddModelSettings(RpcParameters? p, Dictionary<string, object?> body)
        {
            // An empty string clears a model or setting (the scope then uses the server default).
            string? inferenceEndpointId = p?.GetString("inferenceEndpointId");
            if (inferenceEndpointId != null) body["inferenceEndpointId"] = inferenceEndpointId.Length > 0 ? inferenceEndpointId : null;
            string? queryEndpointId = p?.GetString("queryEndpointId");
            if (queryEndpointId != null) body["queryEndpointId"] = queryEndpointId.Length > 0 ? queryEndpointId : null;
            string? queryExpansion = p?.GetString("queryExpansion");
            if (queryExpansion != null) body["queryExpansion"] = queryExpansion.Length > 0 ? queryExpansion : null;
            bool? conversationRewrite = p?.GetBoolean("conversationRewrite");
            if (conversationRewrite.HasValue) body["conversationRewrite"] = conversationRewrite.Value;
            bool? queryDecomposition = p?.GetBoolean("queryDecomposition");
            if (queryDecomposition.HasValue) body["queryDecomposition"] = queryDecomposition.Value;
        }

        private static void AddRerankSettings(RpcParameters? p, Dictionary<string, object?> body)
        {
            string? rerankEndpointId = p?.GetString("rerankEndpointId");
            if (rerankEndpointId != null) body["rerankEndpointId"] = rerankEndpointId.Length > 0 ? rerankEndpointId : null;
            long? rerankCandidates = p?.GetInt64("rerankCandidates");
            if (rerankCandidates.HasValue) body["rerankCandidates"] = rerankCandidates.Value;
            double? rerankMinScore = p?.GetDouble("rerankMinScore");
            if (rerankMinScore.HasValue) body["rerankMinScore"] = rerankMinScore.Value;
        }

        private static string BuildEndpointBody(RpcParameters? p)
        {
            Dictionary<string, object?> body = new Dictionary<string, object?>();
            body["name"] = Require(p, "name");
            body["baseUrl"] = Require(p, "baseUrl");
            if (p?.GetString("kind") != null) body["kind"] = p.GetString("kind");
            if (p?.GetString("apiFormat") != null) body["apiFormat"] = p.GetString("apiFormat");
            if (p?.GetString("authType") != null) body["authType"] = p.GetString("authType");
            if (p?.GetString("authHeaderName") != null) body["authHeaderName"] = p.GetString("authHeaderName");
            if (p?.GetString("authSecretHeaderName") != null) body["authSecretHeaderName"] = p.GetString("authSecretHeaderName");
            if (p?.GetString("authQueryParam") != null) body["authQueryParam"] = p.GetString("authQueryParam");
            if (p?.GetString("authKeyId") != null) body["authKeyId"] = p.GetString("authKeyId");
            if (p?.GetString("authSecret") != null) body["authSecret"] = p.GetString("authSecret");
            if (p?.GetString("model") != null) body["model"] = p.GetString("model");
            long? dimensionality = p?.GetInt64("dimensionality");
            if (dimensionality.HasValue) body["dimensionality"] = dimensionality.Value;
            if (p?.GetString("healthCheckUrl") != null) body["healthCheckUrl"] = p.GetString("healthCheckUrl");
            if (p?.GetString("reasoning") != null) body["reasoning"] = p.GetString("reasoning");
            bool? active = p?.GetBoolean("active");
            if (active.HasValue) body["active"] = active.Value;
            return JsonSerializer.Serialize(body);
        }

        private static object EndpointProperties()
        {
            return new
            {
                tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." },
                endpointId = new { type = "string", description = "Endpoint id (update only)." },
                name = new { type = "string" },
                kind = new { type = "string", description = "Embedding or Inference. Every model that is not an embedding model is an inference endpoint, rerankers included (Rerank is accepted and stored as Inference)." },
                apiFormat = new { type = "string", description = "Ollama, OpenAI, VLlm, or Gemini for embedding and chat models; Tei or Cohere for a cross-encoder, which can only rerank. A chat model in Ollama, OpenAI, or VLlm format can also rerank." },
                baseUrl = new { type = "string", description = "Full base URL; the API path is appended (e.g. http://host:11434 or https://api.openai.com)." },
                authType = new { type = "string", description = "None, BearerToken, ApiKeyHeader, QueryParam, BasicAuth, or AccessKeySecret." },
                authHeaderName = new { type = "string", description = "Header name for ApiKeyHeader, or access-key header for AccessKeySecret." },
                authSecretHeaderName = new { type = "string", description = "Secret-key header name for AccessKeySecret." },
                authQueryParam = new { type = "string", description = "Query-string parameter name for QueryParam auth (e.g. key)." },
                authKeyId = new { type = "string", description = "Username (BasicAuth) or access key (AccessKeySecret)." },
                authSecret = new { type = "string", description = "Bearer token / header value / query value / password / secret key." },
                model = new { type = "string" },
                dimensionality = new { type = "integer", description = "Embedding vector dimension (embedding endpoints)." },
                healthCheckUrl = new { type = "string", description = "Health check target: a path appended to baseUrl (e.g. /api/tags) or a full http(s) URL used as-is." },
                reasoning = new { type = "string", @enum = new[] { "Default", "Off", "Low", "Medium", "High" }, description = "How much a reasoning model thinks on Isis's calls to an inference endpoint (answers, query steps, reranking). Default sends nothing." },
                active = new { type = "boolean" }
            };
        }

        private void RegisterTools()
        {
            AddTool(
                "session_start",
                Describe("session_start"),
                new
                {
                    type = "object",
                    properties = new
                    {
                        project = new { type = "string", description = "The git repository name if there is one (from the origin remote), else the project or working directory name; matched to a scope ignoring case and punctuation." },
                        remote = new { type = "string", description = "Optional git remote URL; its repository name is tried after project." },
                        directory = new { type = "string", description = "Optional working directory name, tried last; it finds a scope but never creates one." },
                        createIfMissing = new { type = "boolean", description = "Create the project's scope when none matches (default true)." },
                        maxMemories = new { type = "integer", description = "How many recent memories to include, 0 to 100 (default 15)." }
                    },
                    required = Array.Empty<string>()
                },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    Dictionary<string, object?> body = new Dictionary<string, object?>();
                    if (p?.GetString("project") != null) body["project"] = p.GetString("project");
                    if (p?.GetString("remote") != null) body["remote"] = p.GetString("remote");
                    if (p?.GetString("directory") != null) body["directory"] = p.GetString("directory");
                    bool? createIfMissing = p?.GetBoolean("createIfMissing");
                    if (createIfMissing.HasValue) body["createIfMissing"] = createIfMissing.Value;
                    long? maxMemories = p?.GetInt64("maxMemories");
                    if (maxMemories.HasValue) body["maxMemories"] = maxMemories.Value;
                    return await ProxyAsync(HttpMethod.Post, "/v1.0/api/session", JsonSerializer.Serialize(body), "session_start", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "whoami",
                Describe("whoami"),
                new { type = "object", properties = new { } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Get, "/v1.0/api/whoami", null, "whoami", CurrentCredentials(), ct).ConfigureAwait(false));

            AddTool(
                "instructions",
                Describe("instructions"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, scopeId = new { type = "string", description = "Optional scope identifier; resolves that scope's effective instructions." } }, required = Array.Empty<string>() },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    string tenantId = Encode(await TenantAsync(p, ct).ConfigureAwait(false));
                    string? scopeId = p?.GetString("scopeId");
                    string path = string.IsNullOrEmpty(scopeId)
                        ? "/v1.0/api/tenants/" + tenantId + "/instructions"
                        : "/v1.0/api/tenants/" + tenantId + "/scopes/" + Encode(scopeId) + "/effective-instructions";
                    return await ProxyAsync(HttpMethod.Get, path, null, "instructions", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "scope_enumerate",
                Describe("scope_enumerate"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." } }, required = Array.Empty<string>() },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Get, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes", null, "scope_enumerate", CurrentCredentials(), ct).ConfigureAwait(false));

            AddTool(
                "scope_create",
                Describe("scope_create"),
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." },
                        name = new { type = "string", description = "Unique scope name within the tenant (e.g. the project name)." },
                        description = new { type = "string" },
                        storeProvider = new { type = "string", description = "RecallDb or Filesystem (Verbex is not available yet). Defaults to RecallDb." },
                        embeddingEndpointId = new { type = "string", description = "Embedding endpoint id for RecallDb semantic scopes." },
                        dimensionality = new { type = "integer", description = "Embedding vector dimension for RecallDb scopes." },
                        filesystemLayout = new { type = "string", description = "SingleFile, Hierarchy, or OkfBundle (Open Knowledge Format), for Filesystem scopes." },
                        targetPath = new { type = "string", description = "Directory or file path, for Filesystem scopes." },
                        chunkingMode = new { type = "string", description = "When to chunk oversized bodies for embedding: OnOverflow (default), Always, or Off." },
                        chunkStrategy = new { type = "string", description = "Chunk splitting strategy, for example FixedTokenCount (default), SentenceBased, ParagraphBased, Recursive." },
                        chunkMaxTokens = new { type = "integer", description = "Per-chunk token budget (0 = the embedding model's budget)." },
                        chunkOverlapTokens = new { type = "integer", description = "Token overlap between adjacent chunks (default 64)." },
                        rerankEndpointId = new { type = "string", description = "Optional inference endpoint that reranks the scope's searches: a cross-encoder (Tei or Cohere format), or a chat model for a slower, high-precision mode. Default: the tenant's first cross-encoder." },
                        rerankCandidates = new { type = "integer", description = "Candidates sent to the reranker (1..100, default 10)." },
                        rerankMinScore = new { type = "number", description = "Drop reranked hits scoring below this (0..1). Omit to keep all." },
                        inferenceEndpointId = new { type = "string", description = "Inference endpoint id that answers chat in this scope (empty string clears it; default: the tenant's first active inference endpoint)." },
                        queryEndpointId = new { type = "string", description = "Inference endpoint id that rewrites follow-ups, splits, and expands queries (empty string clears it; default: the scope's chat model)." },
                        conversationRewrite = new { type = "boolean", description = "Rewrite chat follow-up questions into standalone queries (default: the server setting, on)." },
                        queryExpansion = new { type = "string", @enum = new[] { "Off", "On", "Auto", "" }, description = "Expand queries with a drafted answer and keywords: Off, On, or Auto (when not reranked). Empty string uses the server default (Auto)." },
                        queryDecomposition = new { type = "boolean", description = "Split multi-part questions into sub-queries (default: the server setting, off)." }
                    },
                    required = new[] { "name" }
                },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    Dictionary<string, object?> body = new Dictionary<string, object?>();
                    body["name"] = Require(p, "name");
                    if (p?.GetString("description") != null) body["description"] = p.GetString("description");
                    if (p?.GetString("storeProvider") != null) body["storeProvider"] = p.GetString("storeProvider");
                    if (p?.GetString("embeddingEndpointId") != null) body["embeddingEndpointId"] = p.GetString("embeddingEndpointId");
                    long? dimensionality = p?.GetInt64("dimensionality");
                    if (dimensionality.HasValue) body["dimensionality"] = dimensionality.Value;
                    if (p?.GetString("filesystemLayout") != null) body["filesystemLayout"] = p.GetString("filesystemLayout");
                    if (p?.GetString("targetPath") != null) body["targetPath"] = p.GetString("targetPath");
                    if (p?.GetString("chunkingMode") != null) body["chunkingMode"] = p.GetString("chunkingMode");
                    if (p?.GetString("chunkStrategy") != null) body["chunkStrategy"] = p.GetString("chunkStrategy");
                    long? chunkMaxTokens = p?.GetInt64("chunkMaxTokens");
                    if (chunkMaxTokens.HasValue) body["chunkMaxTokens"] = chunkMaxTokens.Value;
                    long? chunkOverlapTokens = p?.GetInt64("chunkOverlapTokens");
                    if (chunkOverlapTokens.HasValue) body["chunkOverlapTokens"] = chunkOverlapTokens.Value;
                    AddRerankSettings(p, body);
                    AddModelSettings(p, body);
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes";
                    return await ProxyAsync(HttpMethod.Post, path, JsonSerializer.Serialize(body), "scope_create", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "endpoint_enumerate",
                Describe("endpoint_enumerate"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, kind = new { type = "string", description = "Optional filter: Embedding or Inference (rerankers are inference endpoints)." } }, required = Array.Empty<string>() },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/endpoints";
                    string? kind = p?.GetString("kind");
                    if (!string.IsNullOrEmpty(kind)) path += "?kind=" + Encode(kind);
                    return await ProxyAsync(HttpMethod.Get, path, null, "endpoint_enumerate", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "guide",
                Describe("guide"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, scopeId = new { type = "string" } }, required = new[] { "scopeId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Get, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")) + "/guide", null, "guide", CurrentCredentials(), ct).ConfigureAwait(false));

            AddTool(
                "category_enumerate",
                Describe("category_enumerate"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, scopeId = new { type = "string" } }, required = new[] { "scopeId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Get, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")) + "/categories", null, "category_enumerate", CurrentCredentials(), ct).ConfigureAwait(false));

            AddTool(
                "category_create",
                Describe("category_create"),
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." },
                        scopeId = new { type = "string" },
                        name = new { type = "string", description = "Category name (unique within the scope; accepted by memory_search as a filter)." },
                        description = new { type = "string" },
                        instructions = new { type = "string", description = "When and how to write memories in this category." }
                    },
                    required = new[] { "scopeId", "name" }
                },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    Dictionary<string, object?> body = new Dictionary<string, object?>();
                    body["name"] = Require(p, "name");
                    if (p?.GetString("description") != null) body["description"] = p.GetString("description");
                    if (p?.GetString("instructions") != null) body["instructions"] = p.GetString("instructions");
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")) + "/categories";
                    return await ProxyAsync(HttpMethod.Post, path, JsonSerializer.Serialize(body), "category_create", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "memory_enumerate",
                Describe("memory_enumerate"),
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." },
                        scopeId = new { type = "string" },
                        category = new { type = "string", description = "Optional filter by category ID (the cat_ id, not the name)." },
                        maxResults = new { type = "integer" }
                    },
                    required = new[] { "scopeId" }
                },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")) + "/memories";
                    List<string> queryParts = new List<string>();
                    string? category = p?.GetString("category");
                    if (!string.IsNullOrEmpty(category)) queryParts.Add("category=" + Encode(category));
                    long? maxResults = p?.GetInt64("maxResults");
                    if (maxResults.HasValue) queryParts.Add("maxResults=" + maxResults.Value);
                    if (queryParts.Count > 0) path += "?" + string.Join("&", queryParts);
                    return await ProxyAsync(HttpMethod.Get, path, null, "memory_enumerate", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "memory_read",
                Describe("memory_read"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, scopeId = new { type = "string" }, memoryId = new { type = "string" } }, required = new[] { "scopeId", "memoryId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Get, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")) + "/memories/" + Encode(Require(p, "memoryId")), null, "memory_read", CurrentCredentials(), ct).ConfigureAwait(false));

            AddTool(
                "memory_upsert",
                Describe("memory_upsert"),
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." },
                        scopeId = new { type = "string" },
                        category = new { type = "string", description = "Category name (created in the scope if new) or cat_ id." },
                        categoryId = new { type = "string", description = "Alias of category, for a cat_ id." },
                        slug = new { type = "string", description = "Stable, link-addressable slug; re-writing updates in place." },
                        title = new { type = "string" },
                        summary = new { type = "string", description = "One-line recall hook." },
                        body = new { type = "string", description = "The memory content." },
                        type = new { type = "string", description = "Optional classification; one of User, Feedback, Project, Reference. Unknown or omitted values default to Project." },
                        links = new { type = "array", items = new { type = "string" }, description = "Slugs of related memories. Chat follows these (and [[slug]] references in the body) to add linked context." },
                        supersedes = new { type = "array", items = new { type = "string" }, description = "Slugs of memories in this scope that this memory replaces." }
                    },
                    required = new[] { "scopeId", "slug", "body" }
                },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    Dictionary<string, object?> body = new Dictionary<string, object?>();
                    string? category = p?.GetString("category") ?? p?.GetString("categoryId");
                    if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Argument 'category' (a category name or cat_ id) is required.");
                    body["categoryId"] = category;
                    body["slug"] = Require(p, "slug");
                    body["body"] = Require(p, "body");
                    if (p?.GetString("title") != null) body["title"] = p.GetString("title");
                    if (p?.GetString("summary") != null) body["summary"] = p.GetString("summary");
                    if (p?.GetString("type") != null) body["type"] = p.GetString("type");
                    List<string>? links = StringList(p, "links");
                    if (links != null) body["links"] = links;
                    List<string>? supersedes = StringList(p, "supersedes");
                    if (supersedes != null) body["supersedes"] = supersedes;
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")) + "/memories";
                    return await ProxyAsync(HttpMethod.Post, path, JsonSerializer.Serialize(body), "memory_upsert", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "memory_search",
                Describe("memory_search"),
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." },
                        scopeId = new { type = "string" },
                        queryText = new { type = "string" },
                        mode = new { type = "string", description = "Keyword, Semantic, or Hybrid. Semantic/Hybrid require a RecallDB scope." },
                        topK = new { type = "integer" },
                        categoryName = new { type = "string", description = "Optional category filter: the category's name or its cat_ id. An unknown category is an error, not an empty result." },
                        minScore = new { type = "number", description = "Optional minimum score; weaker hits are dropped. Hybrid scores are fused and normalized to 0..1." },
                        recencyWeight = new { type = "number", description = "Hybrid only: weight 0..1 of a signal favoring recently written memories (default 0.1; 0 disables)." },
                        superseded = new { type = "string", description = "Demote (default: a replaced memory ranks right after its replacement), Hide (drop replaced memories), or Include (unchanged order, only marked)." },
                        linkExpansion = new { type = "integer", description = "Add up to this many linked memories (0..10, default 0) after the results that link to them." },
                        diversity = new { type = "number", description = "0..1 (default 0): higher values drop results that repeat higher-ranked ones in favor of other relevant memories." },
                        rerank = new { type = "boolean", description = "Rerank with the scope's rerank endpoint. Default: rerank when the scope has one." },
                        minRerankScore = new { type = "number", description = "Drop reranked hits scoring below this (0..1). Defaults to the scope's rerankMinScore." },
                        additionalQueries = new { type = "array", items = new { type = "string" }, description = "Up to 4 extra queries searched alongside queryText and fused, for example the parts of a multi-part question." },
                        additionalQueryWeight = new { type = "number", description = "Fusion weight of each additional query relative to queryText's 1.0, 0 to 1 (default: the server's setting)." },
                        subQueries = new
                        {
                            type = "array",
                            description = "Up to 4 extra queries with their own fusion weight (0 to 1, default 1) and optional mode (Keyword|Semantic|Hybrid).",
                            items = new { type = "object", properties = new { text = new { type = "string" }, weight = new { type = "number" }, mode = new { type = "string", @enum = new[] { "Keyword", "Semantic", "Hybrid" } } }, required = new[] { "text" } }
                        },
                        decompose = new { type = "boolean", description = "Have the tenant's inference model split a multi-part question into sub-queries before searching (default false)." },
                        expand = new { type = "boolean", description = "Have the tenant's inference model draft a hypothetical answer (searched by vector) and keywords (searched as text), fused below queryText (default false)." },
                        expansionWeight = new { type = "number", description = "Fusion weight of the expand forms relative to queryText's 1.0, 0 to 1 (default: the server's setting)." }
                    },
                    required = new[] { "scopeId", "queryText" }
                },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    Dictionary<string, object?> body = new Dictionary<string, object?>();
                    body["queryText"] = Require(p, "queryText");
                    if (p?.GetString("mode") != null) body["mode"] = p.GetString("mode");
                    long? topK = p?.GetInt64("topK");
                    if (topK.HasValue) body["topK"] = topK.Value;
                    if (p?.GetString("categoryName") != null) body["categoryFilter"] = p.GetString("categoryName");
                    double? minScore = p?.GetDouble("minScore");
                    if (minScore.HasValue) body["minScore"] = minScore.Value;
                    double? recencyWeight = p?.GetDouble("recencyWeight");
                    if (recencyWeight.HasValue) body["recencyWeight"] = recencyWeight.Value;
                    if (p?.GetString("superseded") != null) body["superseded"] = p.GetString("superseded");
                    long? linkExpansion = p?.GetInt64("linkExpansion");
                    if (linkExpansion.HasValue) body["linkExpansion"] = linkExpansion.Value;
                    double? diversity = p?.GetDouble("diversity");
                    if (diversity.HasValue) body["diversity"] = diversity.Value;
                    bool? rerank = p?.GetBoolean("rerank");
                    if (rerank.HasValue) body["rerank"] = rerank.Value;
                    double? minRerankScore = p?.GetDouble("minRerankScore");
                    if (minRerankScore.HasValue) body["minRerankScore"] = minRerankScore.Value;
                    List<string>? additionalQueries = StringList(p, "additionalQueries");
                    if (additionalQueries != null && additionalQueries.Count > 0) body["additionalQueries"] = additionalQueries;
                    bool? decompose = p?.GetBoolean("decompose");
                    if (decompose.HasValue) body["decompose"] = decompose.Value;
                    List<Dictionary<string, object>>? subQueries = SubQueries(p);
                    if (subQueries != null && subQueries.Count > 0) body["subQueries"] = subQueries;
                    bool? expand = p?.GetBoolean("expand");
                    if (expand.HasValue) body["expand"] = expand.Value;
                    double? additionalQueryWeight = p?.GetDouble("additionalQueryWeight");
                    if (additionalQueryWeight.HasValue) body["additionalQueryWeight"] = additionalQueryWeight.Value;
                    double? expansionWeight = p?.GetDouble("expansionWeight");
                    if (expansionWeight.HasValue) body["expansionWeight"] = expansionWeight.Value;
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")) + "/memories/search";
                    return await ProxyAsync(HttpMethod.Post, path, JsonSerializer.Serialize(body), "memory_search", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "memory_delete",
                Describe("memory_delete"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, scopeId = new { type = "string" }, memoryId = new { type = "string" } }, required = new[] { "scopeId", "memoryId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Delete, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")) + "/memories/" + Encode(Require(p, "memoryId")), null, "memory_delete", CurrentCredentials(), ct).ConfigureAwait(false));

            // ---- Scope read/update/delete ----

            AddTool(
                "scope_read",
                Describe("scope_read"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, scopeId = new { type = "string" } }, required = new[] { "scopeId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Get, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")), null, "scope_read", CurrentCredentials(), ct).ConfigureAwait(false));

            AddTool(
                "scope_update",
                Describe("scope_update"),
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." },
                        scopeId = new { type = "string" },
                        name = new { type = "string" },
                        description = new { type = "string" },
                        rerankEndpointId = new { type = "string", description = "Inference endpoint that reranks (a cross-encoder or a chat model), or an empty string to stop reranking." },
                        rerankCandidates = new { type = "integer", description = "Candidates sent to the reranker (1..100)." },
                        rerankMinScore = new { type = "number", description = "Drop reranked hits scoring below this (0..1)." },
                        inferenceEndpointId = new { type = "string", description = "Inference endpoint id that answers chat in this scope (empty string clears it; default: the tenant's first active inference endpoint)." },
                        queryEndpointId = new { type = "string", description = "Inference endpoint id that rewrites follow-ups, splits, and expands queries (empty string clears it; default: the scope's chat model)." },
                        conversationRewrite = new { type = "boolean", description = "Rewrite chat follow-up questions into standalone queries (default: the server setting, on)." },
                        queryExpansion = new { type = "string", @enum = new[] { "Off", "On", "Auto", "" }, description = "Expand queries with a drafted answer and keywords: Off, On, or Auto (when not reranked). Empty string uses the server default (Auto)." },
                        queryDecomposition = new { type = "boolean", description = "Split multi-part questions into sub-queries (default: the server setting, off)." }
                    },
                    required = new[] { "scopeId" }
                },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    // The REST update replaces the whole scope, so start from the current scope and change only what was
                    // passed; sending just the name would clear the embedding endpoint and chunking settings.
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId"));
                    McpCallerCredentials credentials = CurrentCredentials();
                    Dictionary<string, object?> current = (Dictionary<string, object?>)await ProxyAsync(HttpMethod.Get, path, null, "scope_update", credentials, ct).ConfigureAwait(false);
                    if (!(current["success"] is bool ok && ok) || !(current.TryGetValue("data", out object? data) && data is JsonElement element && element.ValueKind == JsonValueKind.Object)) return current;

                    Dictionary<string, object?> body = new Dictionary<string, object?>();
                    foreach (JsonProperty property in element.EnumerateObject()) body[property.Name] = property.Value;
                    if (p?.GetString("name") != null) body["name"] = p.GetString("name");
                    if (p?.GetString("description") != null) body["description"] = p.GetString("description");
                    AddRerankSettings(p, body);
                    AddModelSettings(p, body);
                    return await ProxyAsync(HttpMethod.Put, path, JsonSerializer.Serialize(body), "scope_update", credentials, ct).ConfigureAwait(false);
                });

            AddTool(
                "scope_delete",
                Describe("scope_delete"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, scopeId = new { type = "string" } }, required = new[] { "scopeId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Delete, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")), null, "scope_delete", CurrentCredentials(), ct).ConfigureAwait(false));

            // ---- Category read/update/delete ----

            AddTool(
                "category_read",
                Describe("category_read"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, scopeId = new { type = "string" }, categoryId = new { type = "string" } }, required = new[] { "scopeId", "categoryId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Get, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")) + "/categories/" + Encode(Require(p, "categoryId")), null, "category_read", CurrentCredentials(), ct).ConfigureAwait(false));

            AddTool(
                "category_update",
                Describe("category_update"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, scopeId = new { type = "string" }, categoryId = new { type = "string" }, name = new { type = "string" }, description = new { type = "string" }, instructions = new { type = "string" } }, required = new[] { "scopeId", "categoryId", "name" } },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    Dictionary<string, object?> body = new Dictionary<string, object?>();
                    body["name"] = Require(p, "name");
                    if (p?.GetString("description") != null) body["description"] = p.GetString("description");
                    if (p?.GetString("instructions") != null) body["instructions"] = p.GetString("instructions");
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")) + "/categories/" + Encode(Require(p, "categoryId"));
                    return await ProxyAsync(HttpMethod.Put, path, JsonSerializer.Serialize(body), "category_update", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "category_delete",
                Describe("category_delete"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, scopeId = new { type = "string" }, categoryId = new { type = "string" } }, required = new[] { "scopeId", "categoryId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Delete, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")) + "/categories/" + Encode(Require(p, "categoryId")), null, "category_delete", CurrentCredentials(), ct).ConfigureAwait(false));

            // ---- Model endpoint read/create/update/delete/health ----

            AddTool(
                "endpoint_read",
                Describe("endpoint_read"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, endpointId = new { type = "string" } }, required = new[] { "endpointId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Get, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/endpoints/" + Encode(Require(p, "endpointId")), null, "endpoint_read", CurrentCredentials(), ct).ConfigureAwait(false));

            AddTool(
                "endpoint_create",
                Describe("endpoint_create"),
                new { type = "object", properties = EndpointProperties(), required = new[] { "name", "baseUrl" } },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/endpoints";
                    return await ProxyAsync(HttpMethod.Post, path, BuildEndpointBody(p), "endpoint_create", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "endpoint_update",
                Describe("endpoint_update"),
                new { type = "object", properties = EndpointProperties(), required = new[] { "endpointId", "name", "baseUrl" } },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/endpoints/" + Encode(Require(p, "endpointId"));
                    return await ProxyAsync(HttpMethod.Put, path, BuildEndpointBody(p), "endpoint_update", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "endpoint_delete",
                Describe("endpoint_delete"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, endpointId = new { type = "string" } }, required = new[] { "endpointId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Delete, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/endpoints/" + Encode(Require(p, "endpointId")), null, "endpoint_delete", CurrentCredentials(), ct).ConfigureAwait(false));

            AddTool(
                "endpoint_health",
                Describe("endpoint_health"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." } }, required = Array.Empty<string>() },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Get, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/endpoint-health", null, "endpoint_health", CurrentCredentials(), ct).ConfigureAwait(false));

            // ---- Chat with memory ----

            AddTool(
                "chat",
                Describe("chat"),
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." },
                        scopeId = new { type = "string" },
                        question = new { type = "string" },
                        topK = new { type = "integer" },
                        inferenceEndpointId = new { type = "string" },
                        history = new
                        {
                            type = "array",
                            description = "Earlier messages in the conversation, oldest first. A follow-up question is rewritten into a standalone query for retrieval.",
                            items = new { type = "object", properties = new { role = new { type = "string", @enum = new[] { "user", "assistant" } }, content = new { type = "string" } }, required = new[] { "role", "content" } }
                        }
                    },
                    required = new[] { "scopeId", "question" }
                },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    Dictionary<string, object?> body = new Dictionary<string, object?>();
                    body["question"] = Require(p, "question");
                    long? topK = p?.GetInt64("topK");
                    if (topK.HasValue) body["topK"] = topK.Value;
                    if (p?.GetString("inferenceEndpointId") != null) body["inferenceEndpointId"] = p.GetString("inferenceEndpointId");
                    List<Dictionary<string, string>>? history = ChatHistory(p);
                    if (history != null && history.Count > 0) body["history"] = history;
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/scopes/" + Encode(Require(p, "scopeId")) + "/chat";
                    return await ProxyAsync(HttpMethod.Post, path, JsonSerializer.Serialize(body), "chat", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            // ---- RecallDB collections pass-through ----

            AddTool(
                "collection_enumerate",
                Describe("collection_enumerate"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." } }, required = Array.Empty<string>() },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Get, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/collections", null, "collection_enumerate", CurrentCredentials(), ct).ConfigureAwait(false));

            AddTool(
                "collection_read",
                Describe("collection_read"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, collectionId = new { type = "string" } }, required = new[] { "collectionId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Get, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/collections/" + Encode(Require(p, "collectionId")), null, "collection_read", CurrentCredentials(), ct).ConfigureAwait(false));

            AddTool(
                "collection_create",
                Describe("collection_create"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, name = new { type = "string" }, dimensionality = new { type = "integer" }, description = new { type = "string" } }, required = new[] { "name", "dimensionality" } },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    Dictionary<string, object?> body = new Dictionary<string, object?>();
                    body["name"] = Require(p, "name");
                    long? dim = p?.GetInt64("dimensionality");
                    if (dim.HasValue) body["dimensionality"] = dim.Value;
                    if (p?.GetString("description") != null) body["description"] = p.GetString("description");
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/collections";
                    return await ProxyAsync(HttpMethod.Post, path, JsonSerializer.Serialize(body), "collection_create", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "collection_delete",
                Describe("collection_delete"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, collectionId = new { type = "string" } }, required = new[] { "collectionId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Delete, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/collections/" + Encode(Require(p, "collectionId")), null, "collection_delete", CurrentCredentials(), ct).ConfigureAwait(false));

            // ---- Instruction create/update/delete (tenant-global or scope-specific) ----

            AddTool(
                "instruction_create",
                Describe("instruction_create"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, scopeId = new { type = "string", description = "Omit for tenant-global; set to attach to a scope." }, name = new { type = "string" }, content = new { type = "string" }, mergeMode = new { type = "string", description = "Append, Replace, or Hide (scope instructions)." }, position = new { type = "integer" }, active = new { type = "boolean" } }, required = new[] { "name", "content" } },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    Dictionary<string, object?> body = new Dictionary<string, object?>();
                    body["name"] = Require(p, "name");
                    body["content"] = Require(p, "content");
                    if (p?.GetString("mergeMode") != null) body["mergeMode"] = p.GetString("mergeMode");
                    long? position = p?.GetInt64("position");
                    if (position.HasValue) body["position"] = position.Value;
                    bool? active = p?.GetBoolean("active");
                    if (active.HasValue) body["active"] = active.Value;
                    string tenantId = Encode(await TenantAsync(p, ct).ConfigureAwait(false));
                    string? scopeId = p?.GetString("scopeId");
                    string path = string.IsNullOrEmpty(scopeId)
                        ? "/v1.0/api/tenants/" + tenantId + "/instructions"
                        : "/v1.0/api/tenants/" + tenantId + "/scopes/" + Encode(scopeId) + "/instructions";
                    return await ProxyAsync(HttpMethod.Post, path, JsonSerializer.Serialize(body), "instruction_create", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "instruction_update",
                Describe("instruction_update"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, instructionId = new { type = "string" }, name = new { type = "string" }, content = new { type = "string" }, mergeMode = new { type = "string" }, position = new { type = "integer" }, active = new { type = "boolean" } }, required = new[] { "instructionId", "name", "content" } },
                async (RpcParameters? p, CancellationToken ct) =>
                {
                    Dictionary<string, object?> body = new Dictionary<string, object?>();
                    body["name"] = Require(p, "name");
                    body["content"] = Require(p, "content");
                    if (p?.GetString("mergeMode") != null) body["mergeMode"] = p.GetString("mergeMode");
                    long? position = p?.GetInt64("position");
                    if (position.HasValue) body["position"] = position.Value;
                    bool? active = p?.GetBoolean("active");
                    if (active.HasValue) body["active"] = active.Value;
                    string path = "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/instructions/" + Encode(Require(p, "instructionId"));
                    return await ProxyAsync(HttpMethod.Put, path, JsonSerializer.Serialize(body), "instruction_update", CurrentCredentials(), ct).ConfigureAwait(false);
                });

            AddTool(
                "instruction_delete",
                Describe("instruction_delete"),
                new { type = "object", properties = new { tenantId = new { type = "string", description = "Optional: defaults to the tenant of your credential." }, instructionId = new { type = "string" } }, required = new[] { "instructionId" } },
                async (RpcParameters? p, CancellationToken ct) =>
                    await ProxyAsync(HttpMethod.Delete, "/v1.0/api/tenants/" + Encode(await TenantAsync(p, ct).ConfigureAwait(false)) + "/instructions/" + Encode(Require(p, "instructionId")), null, "instruction_delete", CurrentCredentials(), ct).ConfigureAwait(false));
        }

        #endregion
    }
}
