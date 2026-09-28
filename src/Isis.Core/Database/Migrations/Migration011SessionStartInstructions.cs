namespace Isis.Core.Database.Migrations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Isis.Core.Helpers;
    using Isis.Core.Models;

    /// <summary>
    /// Brings the seeded "Start here" and "Tools" tenant instructions in line with session start. The original text told
    /// agents that every tool needs a tenantId and to begin with whoami, instructions, scope_enumerate, and guide, which
    /// contradicts the protocol session_start now returns next to it. Only an instruction whose content is still exactly
    /// one of the texts Isis has seeded is replaced; one a tenant edited is left alone.
    /// </summary>
    internal sealed class Migration011SessionStartInstructions : ISchemaMigration
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "2026-09-28-session-start-instructions";

        #endregion

        #region Internal-Members

        /// <summary>
        /// Every "Start here" text Isis has seeded, oldest first.
        /// </summary>
        internal static readonly string[] OriginalStartHere = new string[]
        {
            @"This is your operating manual for this tenant's memory in Isis. It is maintained by the tenant and may be
edited, so re-read it (instructions) whenever you begin work.

Naming: the product is 'Isis' (a proper noun — write it 'Isis' or 'isis'). It is NOT an acronym: never write
it as the all-caps 'ISIS', which refers to something else entirely.

tenantId is required on EVERY tool call except whoami. whoami is the one call that needs no tenantId; its
response gives you the tenantId, which you must then pass to every other tool (scope, category, memory, guide,
endpoint, and instructions tools all take tenantId). Omitting tenantId elsewhere makes the call fail.

First steps, in order:
1. whoami — confirm your tenantId and principal (this is the only call that does not take tenantId).
2. instructions(tenantId) — read this manual (you are here).
3. scope_enumerate(tenantId) — find the scope for your project; if none fits, create one with scope_create.
4. guide(tenantId, scopeId) — for the chosen scope, read its categories and their per-category instructions before writing.

Authentication: every call is authenticated with a tenant credential ACCESS KEY, sent as a bearer token
(Authorization: Bearer <accessKey>; the x-access-key header is also accepted). The access key is public and
transferable; the secret key never leaves your MCP client. Calls without an access key are rejected.",
            @"This is your operating manual for this tenant's memory. It is maintained by the tenant and may be
edited, so re-read it (isis_instructions) whenever you begin work.

First steps, in order:
1. isis_whoami — confirm your tenantId and principal.
2. isis_instructions — read this manual (you are here).
3. isis_scope_enumerate — find the scope for your project; if none fits, create one with isis_scope_create.
4. isis_guide — for the chosen scope, read its categories and their per-category instructions before writing.

Authentication: every call is authenticated with a tenant credential ACCESS KEY, sent as a bearer token
(Authorization: Bearer <accessKey>; the x-access-key header is also accepted). The access key is public and
transferable; the secret key never leaves your MCP client. Calls without an access key are rejected."
        };

        /// <summary>
        /// Every "Tools" text Isis has seeded, oldest first.
        /// </summary>
        internal static readonly string[] OriginalTools = new string[]
        {
            @"Your MCP client also receives each tool's full input schema from the server; this is a quick reference.
- whoami — resolve tenantId and principal.
- instructions(tenantId) — read this manual.
- scope_enumerate(tenantId) — list scopes. scope_create(tenantId, name, [description, storeProvider,
  embeddingEndpointId, dimensionality, filesystemLayout, targetPath]) — create a project scope if missing. The
  default store (RecallDb) auto-selects the tenant's embedding endpoint and its dimensionality; if the tenant
  has none, create a Filesystem (keyword-only) scope instead.
- endpoint_enumerate(tenantId, [kind=Embedding|Inference|Rerank]): list model endpoints (id, model, dimensionality)
  to choose an embeddingEndpointId, or to confirm whether semantic (RecallDb) scopes are available at all.
- guide(tenantId, scopeId) — a scope's categories, their instructions, and store capabilities. Call first.
- category_enumerate(tenantId, scopeId) / category_create(tenantId, scopeId, name, [description, instructions]).
- memory_upsert(tenantId, scopeId, categoryId, slug, body, [title, summary, type]) — idempotent on (scope, category, slug). 'type' is optional (User|Feedback|Project|Reference; unknown defaults to Project).
- memory_search(tenantId, scopeId, queryText, [mode=Keyword|Semantic|Hybrid, topK, categoryName]) — note: search filters by category NAME; enumerate filters by category id.
- memory_enumerate / memory_read / memory_delete.",
            @"Your MCP client also receives each tool's full input schema from the server; this is a quick reference.
- whoami — resolve tenantId and principal.
- instructions(tenantId) — read this manual.
- scope_enumerate(tenantId) — list scopes. scope_create(tenantId, name, [description, storeProvider,
  embeddingEndpointId, dimensionality, filesystemLayout, targetPath]) — create a project scope if missing. The
  default store (RecallDb) auto-selects the tenant's embedding endpoint and its dimensionality; if the tenant
  has none, create a Filesystem or Verbex (keyword-only) scope instead.
- endpoint_enumerate(tenantId, [kind=Embedding|Inference]) — list model endpoints (id, model, dimensionality)
  to choose an embeddingEndpointId, or to confirm whether semantic (RecallDb) scopes are available at all.
- guide(tenantId, scopeId) — a scope's categories, their instructions, and store capabilities. Call first.
- category_enumerate(tenantId, scopeId) / category_create(tenantId, scopeId, name, [description, instructions]).
- memory_upsert(tenantId, scopeId, categoryId, slug, body, [title, summary, type]) — idempotent on (scope, category, slug). 'type' is optional (User|Feedback|Project|Reference; unknown defaults to Project).
- memory_search(tenantId, scopeId, queryText, [mode=Keyword|Semantic|Hybrid, topK, categoryName]) — note: search filters by category NAME; enumerate filters by category id.
- memory_enumerate / memory_read / memory_delete.",
            @"Your MCP client also receives each tool's full input schema from the server; this is a quick reference.
- isis_whoami — resolve tenantId and principal.
- isis_instructions(tenantId) — read this manual.
- isis_scope_enumerate(tenantId) — list scopes. isis_scope_create(tenantId, name, [description, storeProvider,
  embeddingEndpointId, dimensionality, filesystemLayout, targetPath]) — create a project scope if missing. The
  default store (RecallDb) auto-selects the tenant's embedding endpoint and its dimensionality; if the tenant
  has none, create a Filesystem or Verbex (keyword-only) scope instead.
- isis_endpoint_enumerate(tenantId, [kind=Embedding|Inference]) — list model endpoints (id, model, dimensionality)
  to choose an embeddingEndpointId, or to confirm whether semantic (RecallDb) scopes are available at all.
- isis_guide(tenantId, scopeId) — a scope's categories, their instructions, and store capabilities. Call first.
- isis_category_enumerate(tenantId, scopeId) / isis_category_create(tenantId, scopeId, name, [description, instructions]).
- isis_memory_upsert(tenantId, scopeId, categoryId, slug, body, [title, summary, type]) — idempotent on (scope, category, slug). 'type' is optional (User|Feedback|Project|Reference; unknown defaults to Project).
- isis_memory_search(tenantId, scopeId, queryText, [mode=Keyword|Semantic|Hybrid, topK, categoryName]) — note: search filters by category NAME; enumerate filters by category id.
- isis_memory_enumerate / isis_memory_read / isis_memory_delete."
        };

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task ApplyAsync(DatabaseDriverBase driver, Func<CancellationToken, Task> ensureSchema, CancellationToken token)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));

            EnumerationQuery all = new EnumerationQuery { MaxResults = 1000 };
            List<Tenant> tenants = (await driver.Tenants.EnumerateAsync(all, token).ConfigureAwait(false)).Objects;
            foreach (Tenant tenant in tenants)
            {
                List<Instruction> instructions = (await driver.Instructions.EnumerateAsync(tenant.Id, null, all, token).ConfigureAwait(false)).Objects;
                foreach (Instruction instruction in instructions)
                {
                    string? replacement = null;
                    if (instruction.Name == "Start here" && Matches(instruction.Content, OriginalStartHere)) replacement = DefaultInstructionText.StartHere;
                    else if (instruction.Name == "Tools" && Matches(instruction.Content, OriginalTools)) replacement = DefaultInstructionText.Tools;
                    if (replacement == null) continue;

                    instruction.Content = replacement;
                    await driver.Instructions.UpdateAsync(instruction, token).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Private-Methods

        private static bool Matches(string? stored, string[] originals)
        {
            // Line endings may differ with how the source was checked out when the text was seeded.
            string normalized = (stored ?? string.Empty).Replace("\r\n", "\n").Trim();
            foreach (string original in originals)
            {
                if (string.Equals(normalized, original.Replace("\r\n", "\n").Trim(), StringComparison.Ordinal)) return true;
            }

            return false;
        }

        #endregion
    }
}
