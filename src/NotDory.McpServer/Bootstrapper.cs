namespace NotDory.McpServer
{
    using System;
    using System.Threading;
    using NotDory.McpServer.Settings;

    /// <summary>
    /// Composition root for the MCP server. Loads settings, applies environment overrides, starts the MCP
    /// transport, and blocks until shutdown.
    /// </summary>
    public static class Bootstrapper
    {
        #region Public-Methods

        /// <summary>
        /// Run the MCP server until interrupted.
        /// </summary>
        /// <param name="args">Command-line arguments. The first argument, if present, is the settings file path.</param>
        public static void Run(string[] args)
        {
            string settingsFile = ResolveSettingsFile(args);
            McpServerSettings settings = McpServerSettings.FromFile(settingsFile);
            settings.ToFile(settingsFile);
            ApplyEnvironmentOverrides(settings);

            Console.WriteLine("[NotDory.Mcp] starting, proxying REST at " + settings.RestBaseUrl());

            using CancellationTokenSource cts = new CancellationTokenSource();
            NotDoryMcpServer server = new NotDoryMcpServer(settings);
            server.Start(cts.Token);
            Console.WriteLine("[NotDory.Mcp] MCP server listening on http://" + settings.Hostname + ":" + settings.Port + settings.McpPath + " (streamable HTTP + SSE)");

            ManualResetEventSlim shutdown = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                shutdown.Set();
            };
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => shutdown.Set();

            shutdown.Wait();

            Console.WriteLine("[NotDory.Mcp] stopping");
            server.Stop();
            server.Dispose();
            cts.Cancel();
        }

        #endregion

        #region Private-Methods

        private static string ResolveSettingsFile(string[] args)
        {
            string? fromEnv = Environment.GetEnvironmentVariable("NOTDORY_MCP_SETTINGS_FILE");
            if (!String.IsNullOrEmpty(fromEnv)) return fromEnv;
            if (args != null && args.Length > 0 && !String.IsNullOrEmpty(args[0])) return args[0];
            return "notdory.mcp.json";
        }

        private static void ApplyEnvironmentOverrides(McpServerSettings settings)
        {
            string? port = Environment.GetEnvironmentVariable("NOTDORY_MCP_PORT");
            if (!String.IsNullOrEmpty(port) && Int32.TryParse(port, out int p)) settings.Port = p;

            string? host = Environment.GetEnvironmentVariable("NOTDORY_MCP_HOSTNAME");
            if (!String.IsNullOrEmpty(host)) settings.Hostname = host;

            string? restHost = Environment.GetEnvironmentVariable("NOTDORY_MCP_REST_HOSTNAME");
            if (!String.IsNullOrEmpty(restHost)) settings.RestHostname = restHost;

            string? restPort = Environment.GetEnvironmentVariable("NOTDORY_MCP_REST_PORT");
            if (!String.IsNullOrEmpty(restPort) && Int32.TryParse(restPort, out int rp)) settings.RestPort = rp;
        }

        #endregion
    }
}
