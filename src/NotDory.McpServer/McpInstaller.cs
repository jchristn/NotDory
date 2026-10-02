namespace NotDory.McpServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using NotDory.McpServer.Settings;

    /// <summary>
    /// Automates connecting an agent client (Claude Code, or a project <c>.mcp.json</c>) to the NotDory MCP
    /// server by upserting a "notdory" entry into the client's configuration. Other servers and unknown keys
    /// are preserved, and the target file is backed up before writing.
    /// </summary>
    public static class McpInstaller
    {
        #region Public-Methods

        /// <summary>
        /// Run the installer with the given command-line arguments (those following the "install" verb).
        /// </summary>
        /// <param name="args">The install arguments.</param>
        /// <returns>A process exit code.</returns>
        public static int Run(string[] args)
        {
            string settingsFile = Environment.GetEnvironmentVariable("NOTDORY_MCP_SETTINGS_FILE") ?? "notdory.mcp.json";
            McpServerSettings settings = McpServerSettings.FromFile(settingsFile);

            string host = settings.Hostname == "*" || string.IsNullOrEmpty(settings.Hostname) ? "127.0.0.1" : settings.Hostname;
            int port = settings.Port;
            string accessKey = "notdorydefaultkey";
            bool project = false;
            bool sessionHook = true;
            string? explicitUrl = null;
            string? restUrl = Environment.GetEnvironmentVariable("NOTDORY_REST_URL");

            string? envHost = Environment.GetEnvironmentVariable("NOTDORY_MCP_HOSTNAME");
            if (!string.IsNullOrEmpty(envHost) && envHost != "*") host = envHost;
            string? envPort = Environment.GetEnvironmentVariable("NOTDORY_MCP_PORT");
            if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out int ep)) port = ep;
            string? envAccessKey = Environment.GetEnvironmentVariable("NOTDORY_MCP_ACCESS_KEY") ?? Environment.GetEnvironmentVariable("NOTDORY_AUTH_DEFAULT_ACCESS_KEY");
            if (!string.IsNullOrEmpty(envAccessKey)) accessKey = envAccessKey;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--access-key":
                        if (i + 1 < args.Length) accessKey = args[++i];
                        break;
                    case "--port":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int p)) { port = p; i++; }
                        break;
                    case "--host":
                        if (i + 1 < args.Length) host = args[++i];
                        break;
                    case "--url":
                        if (i + 1 < args.Length) explicitUrl = args[++i];
                        break;
                    case "--project":
                        project = true;
                        break;
                    case "--rest-url":
                        if (i + 1 < args.Length) restUrl = args[++i];
                        break;
                    case "--no-session-hook":
                        sessionHook = false;
                        break;
                }
            }

            string url = explicitUrl ?? ("http://" + host + ":" + port + settings.McpPath);
            string target = ResolveTarget(project);

            // Authenticate with the credential ACCESS KEY only. The access key is the public, transferable
            // material; the secret key is never written into a client config and never leaves the client.
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                ["x-access-key"] = accessKey
            };

            try
            {
                Install(target, url, headers);
                Console.WriteLine("Installed NotDory MCP server 'notdory' -> " + url);
                Console.WriteLine("  config: " + target);
                Console.WriteLine("  x-access-key: " + Mask(accessKey));
                if (sessionHook)
                {
                    // The REST API serves session start; by default it is on the MCP host at the REST port.
                    string rest = string.IsNullOrWhiteSpace(restUrl) ? "http://" + new Uri(url).Host + ":" + settings.RestPort : restUrl!.TrimEnd('/');
                    string hookTarget = ResolveHookTarget(project);
                    InstallSessionHook(hookTarget, rest, accessKey);
                    Console.WriteLine("Installed the NotDory SessionStart hook (session context from " + rest + ") in " + hookTarget);
                    Console.WriteLine("  Skip it with --no-session-hook; set the REST URL with --rest-url.");
                }
                Console.WriteLine("Restart your agent client to pick up the change.");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Install failed: " + e.Message);
                return 1;
            }
        }

        #endregion

        #region Private-Methods

        private static string ResolveTarget(bool project)
        {
            if (project) return Path.Combine(Directory.GetCurrentDirectory(), ".mcp.json");

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string claudeJson = Path.Combine(home, ".claude.json");
            if (File.Exists(claudeJson)) return claudeJson;

            string claudeSettings = Path.Combine(home, ".claude", "settings.json");
            if (File.Exists(claudeSettings)) return claudeSettings;

            return claudeJson;
        }

        /// <summary>
        /// Upsert the "notdory" MCP entry into the given client config file, preserving all other servers and
        /// unknown keys, and writing a backup of any existing file.
        /// </summary>
        /// <param name="target">The config file path.</param>
        /// <param name="url">The MCP endpoint URL.</param>
        /// <param name="headers">The auth headers to write (e.g. x-access-key).</param>
        public static void Install(string target, string url, Dictionary<string, string> headers)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            JsonObject root;
            if (File.Exists(target))
            {
                File.Copy(target, target + ".bak", true);
                string existing = File.ReadAllText(target);
                root = string.IsNullOrWhiteSpace(existing)
                    ? new JsonObject()
                    : (JsonNode.Parse(existing) as JsonObject ?? new JsonObject());
            }
            else
            {
                string? directory = Path.GetDirectoryName(Path.GetFullPath(target));
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
                root = new JsonObject();
            }

            JsonObject servers = root["mcpServers"] as JsonObject ?? new JsonObject();

            McpServerEntry entry = new McpServerEntry();
            entry.Type = "http";
            entry.Url = url;
            entry.Headers = new Dictionary<string, string>(headers);

            servers["notdory"] = JsonNode.Parse(JsonSerializer.Serialize(entry));
            root["mcpServers"] = servers;

            JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(target, root.ToJsonString(options));
        }

        /// <summary>
        /// The shell command the SessionStart hook runs: fetch session start for the project directory as markdown, which
        /// Claude Code adds to the model's context before its first turn. It never fails the session: when NotDory is
        /// unreachable it prints nothing.
        /// </summary>
        /// <param name="restUrl">The NotDory REST base URL, for example http://127.0.0.1:8700.</param>
        /// <param name="accessKey">The credential access key.</param>
        /// <returns>The command.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static string SessionHookCommand(string restUrl, string accessKey)
        {
            if (restUrl == null) throw new ArgumentNullException(nameof(restUrl));
            if (accessKey == null) throw new ArgumentNullException(nameof(accessKey));
            // Send the facts and let the server resolve the project: the git remote's repository name first (stable across
            // clones, whatever the folder is called), then the folder name, which alone never creates a scope. The full path
            // lets a new scope mirror to the project's .okf bundle when the server can see it.
            return "D=\"${CLAUDE_PROJECT_DIR:-$PWD}\"; curl -fsS -m 8 -G -H \"x-access-key: " + accessKey + "\" "
                + "--data-urlencode \"remote=$(git -C \"$D\" remote get-url origin 2>/dev/null)\" --data-urlencode \"directory=$(basename \"$D\")\" --data-urlencode \"path=$D\" "
                + "--data \"format=text\" \"" + restUrl.TrimEnd('/') + "/v1.0/api/session\" || true";
        }

        /// <summary>
        /// Add (or replace) the NotDory SessionStart hook in a Claude Code settings file, keeping every other setting and hook.
        /// The existing file is backed up to <c>.bak</c>.
        /// </summary>
        /// <param name="target">The settings file, for example ~/.claude/settings.json.</param>
        /// <param name="restUrl">The NotDory REST base URL.</param>
        /// <param name="accessKey">The credential access key.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static void InstallSessionHook(string target, string restUrl, string accessKey)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            JsonObject root = ReadOrCreate(target);
            JsonObject hooks = root["hooks"] as JsonObject ?? new JsonObject();
            JsonArray sessionStart = hooks["SessionStart"] as JsonArray ?? new JsonArray();

            // Drop an earlier NotDory hook so re-running the installer updates it instead of adding a second one.
            for (int i = sessionStart.Count - 1; i >= 0; i--)
            {
                if (IsNotDoryHook(sessionStart[i])) sessionStart.RemoveAt(i);
            }

            JsonObject command = new JsonObject { ["type"] = "command", ["command"] = SessionHookCommand(restUrl, accessKey), ["timeout"] = 10 };
            sessionStart.Add(new JsonObject { ["hooks"] = new JsonArray(command) });
            hooks["SessionStart"] = sessionStart;
            root["hooks"] = hooks;
            File.WriteAllText(target, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private static bool IsNotDoryHook(JsonNode? group)
        {
            if (!(group?["hooks"] is JsonArray inner)) return false;
            foreach (JsonNode? hook in inner)
            {
                string command = hook?["command"]?.GetValue<string>() ?? string.Empty;
                if (command.Contains("/v1.0/api/session", StringComparison.Ordinal) && command.Contains("x-access-key", StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static JsonObject ReadOrCreate(string target)
        {
            if (File.Exists(target))
            {
                File.Copy(target, target + ".bak", true);
                string existing = File.ReadAllText(target);
                return string.IsNullOrWhiteSpace(existing) ? new JsonObject() : (JsonNode.Parse(existing) as JsonObject ?? new JsonObject());
            }

            string? directory = Path.GetDirectoryName(Path.GetFullPath(target));
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
            return new JsonObject();
        }

        private static string ResolveHookTarget(bool project)
        {
            if (project) return Path.Combine(Directory.GetCurrentDirectory(), ".claude", "settings.json");
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
        }

        private static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value)) return "(none)";
            if (value.Length <= 4) return "****";
            return new string('*', value.Length - 4) + value.Substring(value.Length - 4);
        }

        #endregion
    }
}
