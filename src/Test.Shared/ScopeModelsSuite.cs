namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using NotDory.Core.Database;
    using NotDory.Core.Database.Migrations;
    using NotDory.Core.Enums;
    using NotDory.Core.Helpers;
    using NotDory.Core.Models;
    using NotDory.Core.Recall;
    using NotDory.Core.Stores;
    using NotDory.Server.Services;
    using Touchstone.Core;

    /// <summary>
    /// Per-scope model and query settings: the chat model, the query model, and the conversation-rewrite,
    /// query-expansion, and query-decomposition switches, with their persistence and how they resolve.
    /// </summary>
    public static class ScopeModelsSuite
    {
        #region Public-Methods

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>The suite.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                "scope-models",
                "Per-scope model and query settings",
                new List<TestCaseDescriptor>
                {
                    TestCase.Sync("scope-models", "defaults", "Scope: model and query settings default to null (server defaults) and clean their input", Defaults),
                    TestCase.Async("scope-models", "round-trip", "Scope model and query settings persist through the database, and clear", RoundTripAsync),
                    TestCase.Async("scope-models", "migration-007", "Migration 007 adds the model and query columns to an older scopes table", Migration007Async),
                    TestCase.Async("scope-models", "switches", "QueryPreparer: request, then scope, then server default decide each step; Auto expands only unreranked searches", SwitchesAsync),
                    TestCase.Async("scope-models", "endpoint-precedence", "QueryPreparer: chat and query models resolve request, scope, chat model, tenant default; inactive and wrong-kind endpoints are skipped", EndpointPrecedenceAsync),
                    TestCase.Async("scope-models", "prepare-applies-steps", "QueryPreparer: expansion adds weighted sub-queries; no model leaves the query unchanged", PrepareAppliesStepsAsync)
                });
        }

        #endregion

        #region Private-Methods

        private static void Defaults()
        {
            Scope scope = new Scope();
            TestCase.Require(scope.InferenceEndpointId == null && scope.QueryEndpointId == null && scope.ConversationRewrite == null && scope.QueryExpansion == null && scope.QueryDecomposition == null, "New scope settings should default to null.");
            scope.InferenceEndpointId = "  ";
            scope.QueryEndpointId = " iep_x ";
            scope.QueryExpansion = (QueryExpansionModeEnum)42;
            TestCase.Require(scope.InferenceEndpointId == null && scope.QueryEndpointId == "iep_x" && scope.QueryExpansion == null, "Blank ids should become null, ids trimmed, and an undefined mode dropped.");
            TestCase.Throws<ArgumentOutOfRangeException>(() => scope.QueryEndpointId = new string('x', 65), "An endpoint id over 64 characters should be rejected.");

            JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            Scope? parsed = JsonSerializer.Deserialize<Scope>("{\"name\":\"s\",\"queryExpansion\":\"On\",\"conversationRewrite\":false,\"queryDecomposition\":true}", options);
            TestCase.Require(parsed != null && parsed.QueryExpansion == QueryExpansionModeEnum.On && parsed.ConversationRewrite == false && parsed.QueryDecomposition == true, "The settings should bind from JSON.");
        }

        private static async Task RoundTripAsync()
        {
            using TempSqlite t = await TempSqlite.CreateAsync().ConfigureAwait(false);
            Tenant tenant = await t.Db.Tenants.CreateAsync(new Tenant { Name = "Acme" }).ConfigureAwait(false);
            Scope scope = await t.Db.Scopes.CreateAsync(new Scope
            {
                TenantId = tenant.Id,
                Name = "p",
                StoreProvider = StoreProviderEnum.Filesystem,
                InferenceEndpointId = "iep_chat",
                QueryEndpointId = "iep_query",
                ConversationRewrite = false,
                QueryExpansion = QueryExpansionModeEnum.On,
                QueryDecomposition = true
            }).ConfigureAwait(false);

            Scope? read = await t.Db.Scopes.ReadAsync(tenant.Id, scope.Id).ConfigureAwait(false);
            TestCase.Require(read != null && read.InferenceEndpointId == "iep_chat" && read.QueryEndpointId == "iep_query", "The model ids should persist.");
            TestCase.Require(read!.ConversationRewrite == false && read.QueryExpansion == QueryExpansionModeEnum.On && read.QueryDecomposition == true, "The switches should persist.");

            read.InferenceEndpointId = null;
            read.QueryEndpointId = null;
            read.ConversationRewrite = null;
            read.QueryExpansion = null;
            read.QueryDecomposition = null;
            await t.Db.Scopes.UpdateAsync(read).ConfigureAwait(false);
            Scope? cleared = await t.Db.Scopes.ReadAsync(tenant.Id, scope.Id).ConfigureAwait(false);
            TestCase.Require(cleared != null && cleared.InferenceEndpointId == null && cleared.QueryEndpointId == null && cleared.ConversationRewrite == null && cleared.QueryExpansion == null && cleared.QueryDecomposition == null, "Cleared settings should read back as null.");
        }

        private static async Task Migration007Async()
        {
            using TempSqlite t = await TempSqlite.CreateAsync().ConfigureAwait(false);
            foreach (string column in new[] { "inferenceendpointid", "queryendpointid", "conversationrewrite", "queryexpansion", "querydecomposition" })
                await t.Db.ExecuteQueryAsync("ALTER TABLE scopes DROP COLUMN " + column + ";", true).ConfigureAwait(false);

            Migration007ScopeModels migration = new Migration007ScopeModels();
            await migration.ApplyAsync(t.Db, _ => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);
            await migration.ApplyAsync(t.Db, _ => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);
            await t.Db.ExecuteQueryAsync("SELECT inferenceendpointid, queryendpointid, conversationrewrite, queryexpansion, querydecomposition FROM scopes WHERE 1 = 0;", false).ConfigureAwait(false);
        }

        private static QueryPreparer Preparer(DatabaseDriverBase memoryDatabase, DatabaseDriverBase? endpointDatabase, StubResponseHandler? handler = null)
        {
            InferenceService inference = new InferenceService(handler ?? new StubResponseHandler("{}"));
            return new QueryPreparer(new MemoryService(memoryDatabase), inference, endpointDatabase);
        }

        private static async Task SwitchesAsync()
        {
            using TempSqlite t = await TempSqlite.CreateAsync().ConfigureAwait(false);
            QueryPreparer preparer = Preparer(t.Db, null);
            Scope scope = new Scope();
            TestCase.Require(preparer.ShouldExpand(scope, null, false) && !preparer.ShouldExpand(scope, null, true), "Auto (the default) should expand only searches that are not reranked.");
            TestCase.Require(preparer.ShouldExpand(scope, true, true) && !preparer.ShouldExpand(scope, false, false), "The request should override the scope.");
            scope.QueryExpansion = QueryExpansionModeEnum.On;
            TestCase.Require(preparer.ShouldExpand(scope, null, true), "On should expand reranked searches too.");
            scope.QueryExpansion = QueryExpansionModeEnum.Off;
            TestCase.Require(!preparer.ShouldExpand(scope, null, false), "Off should never expand.");
            preparer.DefaultQueryExpansion = QueryExpansionModeEnum.Off;
            TestCase.Require(!preparer.ShouldExpand(new Scope(), null, false), "A scope without a setting should follow the server default.");

            TestCase.Require(!preparer.ShouldDecompose(new Scope(), null) && preparer.ShouldDecompose(new Scope { QueryDecomposition = true }, null) && !preparer.ShouldDecompose(new Scope { QueryDecomposition = true }, false), "Decomposition: server default off, scope on, request off wins.");
            TestCase.Require(preparer.ShouldRewrite(new Scope()) && !preparer.ShouldRewrite(new Scope { ConversationRewrite = false }), "Rewrite: server default on, scope can turn it off.");
        }

        private static async Task EndpointPrecedenceAsync()
        {
            using TempSqlite t = await TempSqlite.CreateAsync().ConfigureAwait(false);
            Tenant tenant = await t.Db.Tenants.CreateAsync(new Tenant { Name = "Acme" }).ConfigureAwait(false);
            ModelEndpoint tenantDefault = await CreateAsync(t, tenant.Id, "default", EndpointKindEnum.Inference, true).ConfigureAwait(false);
            ModelEndpoint chat = await CreateAsync(t, tenant.Id, "chat", EndpointKindEnum.Inference, true).ConfigureAwait(false);
            ModelEndpoint query = await CreateAsync(t, tenant.Id, "query", EndpointKindEnum.Inference, true).ConfigureAwait(false);
            ModelEndpoint inactive = await CreateAsync(t, tenant.Id, "off", EndpointKindEnum.Inference, false).ConfigureAwait(false);
            ModelEndpoint embedding = await CreateAsync(t, tenant.Id, "emb", EndpointKindEnum.Embedding, true).ConfigureAwait(false);
            ModelEndpoint crossEncoder = await CreateAsync(t, tenant.Id, "ce", EndpointKindEnum.Inference, true, ApiFormatEnum.Tei).ConfigureAwait(false);
            QueryPreparer preparer = Preparer(t.Db, t.Db);

            // The tenant-default cases use a second tenant with exactly one active inference endpoint, since "first"
            // follows the enumeration order rather than creation order.
            Tenant other = await t.Db.Tenants.CreateAsync(new Tenant { Name = "Other" }).ConfigureAwait(false);
            await CreateAsync(t, other.Id, "ce", EndpointKindEnum.Inference, true, ApiFormatEnum.Tei).ConfigureAwait(false);
            ModelEndpoint otherDefault = await CreateAsync(t, other.Id, "default", EndpointKindEnum.Inference, true).ConfigureAwait(false);
            ModelEndpoint otherInactive = await CreateAsync(t, other.Id, "off", EndpointKindEnum.Inference, false).ConfigureAwait(false);
            Scope plain = new Scope { TenantId = other.Id, Name = "p" };
            TestCase.Require((await preparer.ResolveChatEndpointAsync(plain, null).ConfigureAwait(false))?.Id == otherDefault.Id, "Without settings, chat should use the tenant's active inference endpoint.");
            Scope staleChat = new Scope { TenantId = other.Id, Name = "s", InferenceEndpointId = otherInactive.Id };
            TestCase.Require((await preparer.ResolveChatEndpointAsync(staleChat, null).ConfigureAwait(false))?.Id == otherDefault.Id, "An inactive scope chat model should fall back to the tenant default.");

            Scope configured = new Scope { TenantId = tenant.Id, Name = "c", InferenceEndpointId = chat.Id, QueryEndpointId = query.Id };
            TestCase.Require((await preparer.ResolveChatEndpointAsync(configured, null).ConfigureAwait(false))?.Id == chat.Id, "Chat should use the scope's chat model.");
            TestCase.Require((await preparer.ResolveQueryEndpointAsync(configured, null, null).ConfigureAwait(false))?.Id == query.Id, "Query steps should use the scope's query model.");
            TestCase.Require((await preparer.ResolveChatEndpointAsync(configured, tenantDefault.Id).ConfigureAwait(false))?.Id == tenantDefault.Id, "A requested endpoint should win.");
            TestCase.Require(await preparer.ResolveChatEndpointAsync(configured, embedding.Id).ConfigureAwait(false) == null, "A requested endpoint of the wrong kind should resolve to nothing.");
            TestCase.Require(await preparer.ResolveChatEndpointAsync(configured, crossEncoder.Id).ConfigureAwait(false) == null, "A cross-encoder cannot answer chat.");

            Scope chatOnly = new Scope { TenantId = tenant.Id, Name = "o", InferenceEndpointId = chat.Id };
            TestCase.Require((await preparer.ResolveQueryEndpointAsync(chatOnly, null, null).ConfigureAwait(false))?.Id == chat.Id, "Without a query model, query steps should use the scope's chat model.");

            Scope stale = new Scope { TenantId = tenant.Id, Name = "s", InferenceEndpointId = inactive.Id, QueryEndpointId = embedding.Id };
            TestCase.Require((await preparer.ResolveQueryEndpointAsync(stale, null, chat).ConfigureAwait(false))?.Id == chat.Id, "Inactive or wrong-kind scope endpoints should be skipped in favor of the chat model in use.");
        }

        private static async Task<ModelEndpoint> CreateAsync(TempSqlite t, string tenantId, string name, EndpointKindEnum kind, bool active, ApiFormatEnum format = ApiFormatEnum.OpenAI)
        {
            ModelEndpoint endpoint = new ModelEndpoint { TenantId = tenantId, Name = name, Kind = kind, ApiFormat = format, BaseUrl = "http://127.0.0.1:9", Model = "m", Active = active };
            endpoint.Id = IdGenerator.Endpoint(kind);
            await Task.Delay(5).ConfigureAwait(false);
            return await t.Db.ModelEndpoints.CreateAsync(endpoint).ConfigureAwait(false);
        }

        private static async Task PrepareAppliesStepsAsync()
        {
            string reply = JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content = "{\"answer\": \"The staging database is pg-staging-01.\", \"keywords\": [\"staging\", \"database\"]}" } } } });
            using StubResponseHandler handler = new StubResponseHandler(reply);
            using TempSqlite t = await TempSqlite.CreateAsync().ConfigureAwait(false);
            QueryPreparer preparer = Preparer(t.Db, null, handler);
            ModelEndpoint endpoint = new ModelEndpoint { Name = "q", Kind = EndpointKindEnum.Inference, ApiFormat = ApiFormatEnum.OpenAI, BaseUrl = "http://127.0.0.1:9", Model = "m" };

            MemorySearchQuery query = new MemorySearchQuery { QueryText = "where does staging keep its data?", Mode = SearchModeEnum.Hybrid, ExpansionWeight = 0.4 };
            QueryPreparation preparation = await preparer.PrepareAsync(query, endpoint, null, true, false, true).ConfigureAwait(false);
            TestCase.Require(preparation.Expanded && query.SubQueries != null && query.SubQueries.Count == 2 && query.SubQueries.All(s => s.Weight == 0.4), "Expansion should add the answer and keywords at the requested weight.");
            TestCase.Require(query.SubQueries![0].Mode == SearchModeEnum.Semantic && query.SubQueries[1].Mode == SearchModeEnum.Keyword, "The answer should be searched by vector and the keywords as text.");
            TestCase.Require(preparation.StandaloneQuestion == null, "Without history there is nothing to rewrite.");

            MemorySearchQuery untouched = new MemorySearchQuery { QueryText = "x" };
            QueryPreparation none = await preparer.PrepareAsync(untouched, null, null, true, true, true).ConfigureAwait(false);
            TestCase.Require(!none.Expanded && untouched.SubQueries == null && untouched.AdditionalQueries == null, "Without a model the query should be unchanged.");
        }

        #endregion
    }
}
