namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Text.Json;
    using Isis.Core.Database;
    using Isis.Core.Enums;
    using Isis.Core.Helpers;
    using Isis.Core.Models;
    using Isis.Core.Recall;
    using Isis.Core.Stores;
    using Isis.Core.Stores.RecallDb;
    using Isis.Server.Models;
    using Isis.Server.Routes;
    using Isis.Server.Services;
    using Isis.Server.Settings;
    using Touchstone.Core;

    /// <summary>
    /// Input and settings validation: numbers clamp (NaN and infinity fall back to a default), oversized strings and lists
    /// are rejected, null lists and sections become empty or default, and undefined enum values fall back.
    /// </summary>
    public static class ValidationSuite
    {
        #region Public-Methods

        /// <summary>
        /// Build the validation suite.
        /// </summary>
        /// <returns>The suite.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                "validation",
                "Input and settings validation",
                new List<TestCaseDescriptor>
                {
                    TestCase.Sync("validation", "input-guard", "InputGuard: length and count limits, list cleanup, NaN-safe clamps, enum fallback", InputGuardCase),
                    TestCase.Sync("validation", "search-query", "MemorySearchQuery: clamps numbers, rejects oversized text and lists, drops null sub-queries", SearchQueryCase),
                    TestCase.Sync("validation", "search-query-json", "MemorySearchQuery from JSON: undefined enums fall back, a null sub-query is dropped", SearchQueryJsonCase),
                    TestCase.Sync("validation", "memory", "Memory: null metadata and lists become empty, caps, NaN salience, undefined type", MemoryCase),
                    TestCase.Sync("validation", "chat-request", "ChatRequest and ChatTurn: history cleanup and caps, question length, topK clamp, role normalization", ChatRequestCase),
                    TestCase.Sync("validation", "settings", "Settings: null sections become defaults and out-of-range values clamp", SettingsCase),
                    TestCase.Sync("validation", "services", "Service and core setters reject NaN and out-of-range values", ServicesCase),
                    TestCase.Sync("validation", "endpoint-and-scope", "ModelEndpoint and Scope: health-check, timeout, and chunk settings clamp", EndpointAndScopeCase),
                    TestCase.Sync("validation", "error-classifier", "ErrorClassifier: transient database errors and unreachable services are 503, bad input 400, the rest 500", ErrorClassifierCase)
                });
        }

        #endregion

        #region Private-Methods

        private static void InputGuardCase()
        {
            TestCase.Throws<ArgumentOutOfRangeException>(() => InputGuard.MaxLength(new string('x', 11), 10, "x"), "A string over the limit should be rejected.");
            TestCase.Require(InputGuard.MaxLength(null, 10, "x") == null, "Null should pass a length check.");
            List<string> cleaned = InputGuard.CleanList(new List<string?> { " a ", null, "", "b" }, 5, 10, "x");
            TestCase.Require(string.Join(",", cleaned) == "a,b", "Null and blank entries should be dropped and entries trimmed.");
            TestCase.Require(InputGuard.CleanList(null, 5, 10, "x").Count == 0, "A null list should become empty.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => InputGuard.CleanList(new List<string?> { "a", "b", "c" }, 2, 10, "x"), "Too many entries should be rejected.");
            TestCase.Require(InputGuard.Clamp(double.NaN, 0.0, 1.0, 0.5) == 0.5 && InputGuard.Clamp(double.PositiveInfinity, 0.0, 1.0, 0.25) == 0.25 && InputGuard.Clamp(2.0, 0.0, 1.0, 0.5) == 1.0, "Clamp should fall back for NaN and infinity and clamp the rest.");
            TestCase.Require(InputGuard.Clamp((double?)double.NaN, 0.0, 1.0) == null && InputGuard.Clamp((double?)-3.0, 0.0, 1.0) == 0.0, "Optional clamp should turn NaN into null.");
            TestCase.Require(InputGuard.Defined((SearchModeEnum)99, SearchModeEnum.Hybrid) == SearchModeEnum.Hybrid, "An undefined enum value should fall back.");
        }

        private static void SearchQueryCase()
        {
            MemorySearchQuery query = new MemorySearchQuery();
            query.TokenBudget = int.MaxValue;
            TestCase.Require(query.TokenBudget == 20000, "A huge token budget should clamp to 20000.");
            query.TokenBudget = 0;
            TestCase.Require(query.TokenBudget == null, "A zero token budget should mean the default.");
            query.TextWeight = double.NaN;
            query.RecencyWeight = double.NaN;
            query.Diversity = double.NaN;
            TestCase.Require(query.TextWeight == null && query.RecencyWeight == 0.0 && query.Diversity == 0.0, "NaN weights should fall back.");
            query.MinScore = double.PositiveInfinity;
            query.MinRerankScore = 5.0;
            TestCase.Require(query.MinScore == null && query.MinRerankScore == 1.0, "Scores should clamp and drop infinity.");
            query.QueryText = null!;
            TestCase.Require(query.QueryText == string.Empty, "A null query should become empty (and be rejected by the route).");
            TestCase.Throws<ArgumentOutOfRangeException>(() => query.QueryText = new string('q', 4001), "A query over 4000 characters should be rejected.");
            query.AdditionalQueries = new List<string> { "a", "", null!, " b " };
            TestCase.Require(query.AdditionalQueries!.Count == 2 && query.AdditionalQueries[1] == "b", "Blank additional queries should be dropped.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => query.AdditionalQueries = Enumerable.Range(0, 9).Select(i => "q" + i).ToList(), "More than 8 additional queries should be rejected.");
            query.SubQueries = new List<MemorySubQuery> { null!, new MemorySubQuery { Text = " " }, new MemorySubQuery { Text = "x", Weight = double.NaN } };
            TestCase.Require(query.SubQueries!.Count == 1 && query.SubQueries[0].Weight == 0.0, "Null and blank sub-queries should be dropped and a NaN weight should become 0.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => query.CategoryFilter = new string('c', 257), "A category filter over 256 characters should be rejected.");
        }

        private static void SearchQueryJsonCase()
        {
            JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            MemorySearchQuery? query = JsonSerializer.Deserialize<MemorySearchQuery>("{\"queryText\":\"x\",\"mode\":99,\"superseded\":42,\"subQueries\":[null,{\"text\":\"y\",\"mode\":77}]}", options);
            TestCase.Require(query != null && query.Mode == SearchModeEnum.Hybrid && query.Superseded == SupersededHandlingEnum.Demote, "Undefined enum values in a body should fall back to the defaults.");
            TestCase.Require(query!.SubQueries!.Count == 1 && query.SubQueries[0].Mode == null, "A null sub-query should be dropped and an undefined mode should become null.");
        }

        private static void MemoryCase()
        {
            Memory memory = new Memory { Slug = "s", Body = "b" };
            memory.Metadata = null!;
            memory.Tags = null!;
            memory.Links = new List<string> { "a", null!, " " };
            TestCase.Require(memory.Metadata.Count == 0 && memory.Tags.Count == 0 && memory.Links.Count == 1, "Null metadata and lists should become empty and blank links dropped.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => memory.Tags = Enumerable.Range(0, 257).Select(i => "t" + i).ToList(), "More than 256 tags should be rejected.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => memory.Metadata = Enumerable.Range(0, 129).ToDictionary(i => "k" + i, i => "v"), "More than 128 metadata entries should be rejected.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => memory.Slug = new string('s', 257), "A slug over 256 characters should be rejected.");
            memory.Salience = double.NaN;
            memory.Version = 0;
            memory.Type = (MemoryTypeEnum)99;
            TestCase.Require(memory.Salience == 0.5 && memory.Version == 1 && memory.Type == MemoryTypeEnum.Project, "NaN salience, version 0, and an undefined type should fall back.");
        }

        private static void ChatRequestCase()
        {
            ChatRequest request = new ChatRequest { TopK = 500 };
            TestCase.Require(request.TopK == 100, "topK should clamp to 100.");
            request.History = new List<ChatTurn> { null!, new ChatTurn { Content = "  " }, new ChatTurn { Role = "system", Content = "hi" } };
            TestCase.Require(request.History!.Count == 1 && request.History[0].Role == "user", "Null and empty turns should be dropped and an unknown role read as user.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => request.History = Enumerable.Range(0, 101).Select(i => new ChatTurn { Content = "m" + i }).ToList(), "More than 100 history turns should be rejected.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => request.Question = new string('q', 8001), "A question over 8000 characters should be rejected.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => new ChatTurn { Content = new string('c', 20001) }, "A turn over 20000 characters should be rejected.");
            TestCase.Require(new ChatTurn { Content = null! }.Content == string.Empty, "Null content should become empty.");
        }

        private static void SettingsCase()
        {
            JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            IsisSettings? settings = JsonSerializer.Deserialize<IsisSettings>("{\"rest\":null,\"retrieval\":null,\"requestHistory\":null,\"nodeId\":\"\"}", options);
            TestCase.Require(settings != null && settings.Rest != null && settings.Retrieval != null && settings.RequestHistory != null && settings.NodeId == "isis-1", "Null settings sections should become defaults.");
            RetentionSettings retention = new RetentionSettings { MaxAgeDays = 0, SweepIntervalMinutes = 100000 };
            TestCase.Require(retention.MaxAgeDays == 1 && retention.SweepIntervalMinutes == 1440, "Retention settings should clamp.");
            TestCase.Require(new AuthSettings { SessionLifetimeMinutes = -5 }.SessionLifetimeMinutes == 5, "Session lifetime should clamp to at least 5 minutes.");
            RestSettings rest = new RestSettings { Port = 0, Hostname = " ", MaxRequestBodyBytes = 1 };
            TestCase.Require(rest.Port == 8700 && rest.Hostname == "127.0.0.1" && rest.MaxRequestBodyBytes == 1024, "REST settings should fall back or clamp.");
            TestCase.Require(new RequestHistorySettings { MaxBodyBytes = int.MaxValue }.MaxBodyBytes == 1048576, "Captured body size should clamp to 1 MB.");
            TestCase.Require(new DatabaseSettings { Type = (DatabaseTypeEnum)99 }.Type == DatabaseTypeEnum.Sqlite, "An undefined database type should fall back to Sqlite.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => new RetrievalSettings { ExpansionWeight = double.NaN }, "A NaN weight setting should be rejected.");
            TestCase.Require(new RetrievalSettings { RerankPassageChars = 1000000 }.RerankPassageChars == 20000, "Rerank passage length should clamp to 20000.");
        }

        private static void ServicesCase()
        {
            TestCase.Throws<ArgumentOutOfRangeException>(() => HybridFusion.DefaultTextWeight = double.NaN, "A NaN default text weight should be rejected.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => HybridFusion.DefaultRrfK = 5000, "An RRF constant above 1000 should be rejected.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => MemoryService.QueryFusionRrfK = 5000, "A query fusion constant above 1000 should be rejected.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => MemoryChunker.DefaultChunkFraction = double.NaN, "A NaN chunk fraction should be rejected.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => SearchDiversifier.Diversify(new List<MemorySearchHit>(), double.NaN, 10), "A NaN diversity should be rejected.");
            QueryExpander expander = new QueryExpander(new InferenceService(new System.Net.Http.HttpClientHandler()));
            TestCase.Throws<ArgumentOutOfRangeException>(() => expander.Timeout = TimeSpan.FromHours(1), "A model timeout over 5 minutes should be rejected.");
            EmbeddingModelProfile profile = new EmbeddingModelProfile { ChunkFraction = 5.0, RrfK = 0, TextWeight = double.NaN, Match = null! };
            TestCase.Require(profile.ChunkFraction == 1.0 && profile.RrfK == 1 && profile.TextWeight == null && profile.Match == string.Empty, "Model profile values should clamp.");
        }

        private static void EndpointAndScopeCase()
        {
            ModelEndpoint endpoint = new ModelEndpoint { TimeoutMs = 10, HealthCheckIntervalMs = 0, HealthCheckExpectedStatusCode = 42, HealthyThreshold = 0, HealthCheckUrl = "" };
            TestCase.Require(endpoint.TimeoutMs == 1000 && endpoint.HealthCheckIntervalMs == 1000 && endpoint.HealthCheckExpectedStatusCode == 200 && endpoint.HealthyThreshold == 1 && endpoint.HealthCheckUrl == "/", "Endpoint timing and health-check values should clamp or fall back.");
            TestCase.Require(new ModelEndpoint { TimeoutMs = 0 }.TimeoutMs == 60000, "A zero timeout should mean the default.");
            Scope scope = new Scope { ChunkOverlapTokens = 100000, ChunkMaxTokens = 100000, RerankMinScore = double.NaN, ChunkStrategy = null! };
            TestCase.Require(scope.ChunkOverlapTokens == 1024 && scope.ChunkMaxTokens == 8192 && scope.RerankMinScore == null && scope.ChunkStrategy == "FixedTokenCount", "Scope chunk and rerank settings should clamp or fall back.");
        }

        private static void ErrorClassifierCase()
        {
            TestCase.Require(ErrorClassifier.Classify(new StubDbException("too many clients already", true), false).StatusCode == 503, "A transient database error should be a retryable 503.");
            TestCase.Require(ErrorClassifier.Classify(new StubDbException("syntax error", false), false).StatusCode == 500, "A permanent database error should stay a 500.");
            TestCase.Require(ErrorClassifier.Classify(new ModelEndpointUnavailableException("busy", 429), false).StatusCode == 503, "An unavailable model endpoint should be a 503 even though it is an InvalidOperationException.");
            TestCase.Require(ErrorClassifier.Classify(new InvalidOperationException("bad"), false).StatusCode == 400, "Bad input should be a 400.");
            TestCase.Require(ErrorClassifier.Classify(new HttpRequestException("refused"), false).StatusCode == 503, "An unreachable backing service should be a 503.");
            TestCase.Require(ErrorClassifier.Classify(new TaskCanceledException(), false).StatusCode == 503, "A timeout should be a 503.");
            TestCase.Require(ErrorClassifier.Classify(new TaskCanceledException(), true).StatusCode == 500, "A client cancellation is not a timeout.");
            ErrorClassification fault = ErrorClassifier.Classify(new NullReferenceException("secret detail"), false);
            TestCase.Require(fault.StatusCode == 500 && fault.Error == "InternalError" && !fault.Message.Contains("secret", StringComparison.Ordinal), "An unexpected fault should be a 500 without its details.");
            TestCase.Throws<ArgumentNullException>(() => ErrorClassifier.Classify(null!, false), "A null exception should be rejected.");
        }

        #endregion
    }
}
