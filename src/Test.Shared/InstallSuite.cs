namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using NotDory.McpServer;
    using Touchstone.Core;

    /// <summary>
    /// Automated suite for <see cref="McpInstaller"/>. Verifies that installing the "notdory" MCP entry into an agent
    /// client configuration file writes the expected shape, preserves unrelated servers and unknown keys, backs up
    /// any existing file, honours the chosen auth header, is idempotent, and creates missing directories.
    /// </summary>
    public static class InstallSuite
    {
        #region Public-Methods

        /// <summary>
        /// Get the NotDory MCP install Touchstone test suite.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                "install",
                "NotDory MCP Install Suite",
                new List<TestCaseDescriptor>
                {
                    TestCase.Sync("install", "fresh-install", "Fresh install writes an http notdory entry", FreshInstall),
                    TestCase.Sync("install", "preserve-existing", "Install preserves other servers and unknown keys", PreserveExisting),
                    TestCase.Sync("install", "backup-created", "Install backs up an existing file", BackupCreated),
                    TestCase.Sync("install", "access-key-header", "Install honours the x-access-key header", AccessKeyHeader),
                    TestCase.Sync("install", "idempotent", "Installing twice leaves a single notdory entry", Idempotent),
                    TestCase.Sync("install", "session-hook", "The SessionStart hook is added once, calls session start as text for the project, and keeps other hooks and settings", SessionHook),
                    TestCase.Sync("install", "idempotent-updates-url", "Re-installing updates the notdory url", IdempotentUpdatesUrl),
                    TestCase.Sync("install", "empty-existing-file", "Install repairs an empty existing file", EmptyExistingFile),
                    TestCase.Sync("install", "missing-directory-created", "Install creates a missing target directory", MissingDirectoryCreated)
                });
        }

        #endregion

        #region Private-Methods-Cases

        private static void FreshInstall()
        {
            string tmp = TempFile();
            try
            {
                McpInstaller.Install(tmp, "http://127.0.0.1:8720/mcp", Creds());

                using JsonDocument doc = Load(tmp);
                JsonElement notdory = doc.RootElement.GetProperty("mcpServers").GetProperty("notdory");
                Require(notdory.GetProperty("type").GetString() == "http", "Expected type 'http'.");
                Require(notdory.GetProperty("url").GetString() == "http://127.0.0.1:8720/mcp", "Expected the url to match.");
                JsonElement freshHeaders = notdory.GetProperty("headers");
                Require(freshHeaders.GetProperty("x-access-key").GetString() == "notdorydefaultkey", "Expected the x-access-key header to be 'notdorydefaultkey'.");
                Require(!freshHeaders.TryGetProperty("x-secret-key", out _), "Expected no x-secret-key header (secret must never be written to a client config).");
                Require(!freshHeaders.TryGetProperty("x-api-key", out _), "Expected no x-api-key header.");
            }
            finally
            {
                Cleanup(tmp);
            }
        }

        private static void PreserveExisting()
        {
            string tmp = TempFile();
            try
            {
                File.WriteAllText(tmp, "{\"mcpServers\":{\"other\":{\"command\":\"foo\",\"args\":[\"bar\"]}},\"topKey\":42}");
                McpInstaller.Install(tmp, "http://127.0.0.1:8720/mcp", Creds());

                using JsonDocument doc = Load(tmp);
                JsonElement root = doc.RootElement;

                JsonElement other = root.GetProperty("mcpServers").GetProperty("other");
                Require(other.GetProperty("command").GetString() == "foo", "Expected the 'other' server command to be preserved.");
                Require(other.GetProperty("args")[0].GetString() == "bar", "Expected the 'other' server args to be preserved.");
                Require(root.GetProperty("topKey").GetInt32() == 42, "Expected the unknown top-level key to be preserved.");
                Require(root.GetProperty("mcpServers").TryGetProperty("notdory", out _), "Expected the notdory entry to be added.");
                Require(File.Exists(tmp + ".bak"), "Expected a backup file to be written.");
            }
            finally
            {
                Cleanup(tmp);
            }
        }

        private static void BackupCreated()
        {
            string tmp = TempFile();
            try
            {
                string original = "{\"mcpServers\":{\"other\":{\"command\":\"foo\"}}}";
                File.WriteAllText(tmp, original);
                McpInstaller.Install(tmp, "http://127.0.0.1:8720/mcp", Creds());

                Require(File.Exists(tmp + ".bak"), "Expected a backup file to be written.");
                Require(File.ReadAllText(tmp + ".bak") == original, "Expected the backup to contain the original content.");
            }
            finally
            {
                Cleanup(tmp);
            }
        }

        private static void AccessKeyHeader()
        {
            string tmp = TempFile();
            try
            {
                Dictionary<string, string> headers = new Dictionary<string, string> { ["x-access-key"] = "tok" };
                McpInstaller.Install(tmp, "http://127.0.0.1:8720/mcp", headers);

                using JsonDocument doc = Load(tmp);
                JsonElement written = doc.RootElement.GetProperty("mcpServers").GetProperty("notdory").GetProperty("headers");
                Require(written.GetProperty("x-access-key").GetString() == "tok", "Expected the x-access-key header to be 'tok'.");
                Require(!written.TryGetProperty("x-secret-key", out _), "Expected no x-secret-key header to be written.");
                Require(!written.TryGetProperty("x-api-key", out _), "Expected no x-api-key header when installing an access key.");
            }
            finally
            {
                Cleanup(tmp);
            }
        }

        private static void Idempotent()
        {
            string tmp = TempFile();
            try
            {
                McpInstaller.Install(tmp, "http://127.0.0.1:8720/mcp", Creds());
                McpInstaller.Install(tmp, "http://127.0.0.1:8720/mcp", Creds());

                using JsonDocument doc = Load(tmp);
                JsonElement servers = doc.RootElement.GetProperty("mcpServers");
                int notDoryCount = 0;
                foreach (JsonProperty property in servers.EnumerateObject())
                {
                    if (property.Name == "notdory") notDoryCount++;
                }

                Require(notDoryCount == 1, "Expected exactly one notdory entry after two installs, got " + notDoryCount + ".");
                Require(servers.GetProperty("notdory").GetProperty("url").GetString() == "http://127.0.0.1:8720/mcp", "Expected the url to remain valid.");
            }
            finally
            {
                Cleanup(tmp);
            }
        }

        private static void IdempotentUpdatesUrl()
        {
            string tmp = TempFile();
            try
            {
                McpInstaller.Install(tmp, "http://127.0.0.1:8720/mcp", Creds());
                McpInstaller.Install(tmp, "http://127.0.0.1:9999/mcp", Creds());

                using JsonDocument doc = Load(tmp);
                JsonElement notdory = doc.RootElement.GetProperty("mcpServers").GetProperty("notdory");
                Require(notdory.GetProperty("url").GetString() == "http://127.0.0.1:9999/mcp", "Expected the latest url to win.");
            }
            finally
            {
                Cleanup(tmp);
            }
        }

        private static void EmptyExistingFile()
        {
            string tmp = TempFile();
            try
            {
                File.WriteAllText(tmp, "   ");
                McpInstaller.Install(tmp, "http://127.0.0.1:8720/mcp", Creds());

                using JsonDocument doc = Load(tmp);
                JsonElement notdory = doc.RootElement.GetProperty("mcpServers").GetProperty("notdory");
                Require(notdory.GetProperty("url").GetString() == "http://127.0.0.1:8720/mcp", "Expected a valid notdory entry after repairing an empty file.");
            }
            finally
            {
                Cleanup(tmp);
            }
        }

        private static void MissingDirectoryCreated()
        {
            string dir = Path.Combine(Path.GetTempPath(), "notdory-install-" + Guid.NewGuid().ToString("N").Substring(0, 10));
            string tmp = Path.Combine(dir, ".mcp.json");
            try
            {
                Require(!Directory.Exists(dir), "Test precondition: the target directory should not yet exist.");
                McpInstaller.Install(tmp, "http://127.0.0.1:8720/mcp", Creds());

                Require(File.Exists(tmp), "Expected the config file to be created in the new directory.");
                using JsonDocument doc = Load(tmp);
                Require(doc.RootElement.GetProperty("mcpServers").TryGetProperty("notdory", out _), "Expected the notdory entry to be present.");
            }
            finally
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            }
        }

        #endregion

        #region Private-Methods-Helpers

        private static Dictionary<string, string> Creds()
        {
            return new Dictionary<string, string> { ["x-access-key"] = "notdorydefaultkey" };
        }

        private static string TempFile()
        {
            return Path.Combine(Path.GetTempPath(), "notdory-install-" + Guid.NewGuid().ToString("N") + ".json");
        }

        private static JsonDocument Load(string path)
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }

        private static void Require(bool condition, string message)
        {
            TestCase.Require(condition, message);
        }

        private static void Cleanup(string tmp)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            try { if (File.Exists(tmp + ".bak")) File.Delete(tmp + ".bak"); } catch { }
        }

        private static void SessionHook()
        {
            string dir = Path.Combine(Path.GetTempPath(), "notdory-hook-" + Guid.NewGuid().ToString("N"));
            string target = Path.Combine(dir, ".claude", "settings.json");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, "{\"model\":\"opus\",\"hooks\":{\"SessionStart\":[{\"hooks\":[{\"type\":\"command\",\"command\":\"echo other\"}]}],\"Stop\":[]}}");
                McpInstaller.InstallSessionHook(target, "http://127.0.0.1:8700/", "key123");
                McpInstaller.InstallSessionHook(target, "http://127.0.0.1:8700", "key456");

                JsonObject root = (JsonObject)JsonNode.Parse(File.ReadAllText(target))!;
                JsonArray start = (JsonArray)root["hooks"]!["SessionStart"]!;
                if (root["model"]?.GetValue<string>() != "opus" || root["hooks"]!["Stop"] == null) throw new InvalidOperationException("Other settings and hooks should be kept.");
                if (start.Count != 2) throw new InvalidOperationException("Expected the other hook plus one NotDory hook, got " + start.Count + ".");
                string command = start[1]!["hooks"]![0]!["command"]!.GetValue<string>();
                if (!command.Contains("key456", StringComparison.Ordinal) || command.Contains("key123", StringComparison.Ordinal)) throw new InvalidOperationException("Re-installing should replace the NotDory hook: " + command);
                if (!command.Contains("http://127.0.0.1:8700/v1.0/api/session", StringComparison.Ordinal) || !command.Contains("format=text", StringComparison.Ordinal) || !command.Contains("CLAUDE_PROJECT_DIR", StringComparison.Ordinal) || !command.EndsWith("|| true", StringComparison.Ordinal))
                    throw new InvalidOperationException("The hook should fetch session start as text for the project directory and never fail: " + command);
                if (!File.Exists(target + ".bak")) throw new InvalidOperationException("The settings file should be backed up.");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        #endregion
    }
}
