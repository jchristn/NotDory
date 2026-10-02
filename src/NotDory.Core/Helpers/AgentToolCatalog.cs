namespace NotDory.Core.Helpers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The default description of every NotDory MCP tool, in the order tools/list publishes them. Tool descriptions reach the
    /// model when an agent connects, so an administrator can override each one (the server's agent settings); this catalog
    /// is the text used when no override is set, and the REST API and dashboard show it next to any override.
    /// </summary>
    public static class AgentToolCatalog
    {
        #region Public-Members

        /// <summary>
        /// Tool names and their default descriptions, in publishing order.
        /// </summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> Defaults = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(
                "session_start",
                "Start here, once per session: returns your memory scope for the project (created if new), how to use NotDory, the scope's categories and instructions, and the most recent memories. Pass project as the git repository name (from the origin remote) or, outside a repository, the project or working directory name. No tenantId needed."),
            new KeyValuePair<string, string>(
                "whoami",
                "Show which tenant and principal your credential maps to. Not needed to get started: session_start returns the same, and no tool needs a tenantId."),
            new KeyValuePair<string, string>(
                "instructions",
                "Get the tenant's standing instructions for using memory (session_start already includes the effective set for your scope). Optional: scopeId: when provided, returns the scope's EFFECTIVE instructions (the tenant-global set with the scope's own instructions merged in: appended, overriding, or hiding by name); when omitted, returns the tenant-global set."),
            new KeyValuePair<string, string>(
                "scope_enumerate",
                "List the memory scopes in a tenant."),
            new KeyValuePair<string, string>(
                "scope_create",
                "Create a memory scope for a project when one does not already exist (check first with scope_enumerate). "
                + "Required: name. Optional: description; storeProvider: RecallDb (default: semantic + keyword, needs an embedding endpoint) or Filesystem (keyword-only, git-trackable files). "
                + "For RecallDb you may pass embeddingEndpointId and dimensionality, but if you omit them the tenant's embedding endpoint and its dimensionality are selected AUTOMATICALLY (list options with endpoint_enumerate). "
                + "If the tenant has NO embedding endpoint, RecallDb is rejected with guidance; use storeProvider Filesystem instead. Filesystem also accepts filesystemLayout (SingleFile|Hierarchy|OkfBundle; OkfBundle writes a git-trackable Open Knowledge Format bundle: one markdown file per memory with YAML frontmatter plus a generated index.md) and targetPath. "
                + "To get both, create a RecallDb scope with filesystemMirror true and a targetPath: every memory is written to RecallDB (which serves search) and, concurrently, to an OKF bundle at targetPath (a directory on the NotDory server host, for example inside the repository). "
                + "Optional model and retrieval settings: rerankEndpointId, rerankCandidates, rerankMinScore, inferenceEndpointId (chat model), queryEndpointId (model for query rewriting and expansion), conversationRewrite, queryExpansion (Off|On|Auto), queryDecomposition; unset values use the server defaults."),
            new KeyValuePair<string, string>(
                "endpoint_enumerate",
                "List the tenant's configured model endpoints (embedding and inference, rerankers included), each with its id, kind, API format, model, and embedding dimensionality. Use this to find an embeddingEndpointId (and its dimensionality) BEFORE creating a RecallDb semantic scope. If no embedding endpoint is listed, create a Filesystem (keyword-only) scope instead. Optional: kind (Embedding, Inference, or Rerank)."),
            new KeyValuePair<string, string>(
                "guide",
                "Get a scope's operating guide: categories, their usage instructions, and store capabilities (session_start already includes the categories and instructions). Required: scopeId."),
            new KeyValuePair<string, string>(
                "category_enumerate",
                "List categories in a scope, including their usage instructions. Required: scopeId."),
            new KeyValuePair<string, string>(
                "category_create",
                "Create a category in a scope. Required: scopeId, name. Optional: description, instructions."),
            new KeyValuePair<string, string>(
                "memory_enumerate",
                "List memory summaries in a scope. Required: scopeId. Optional: category (categoryId filter), maxResults."),
            new KeyValuePair<string, string>(
                "memory_read",
                "Read a single memory by id. Required: scopeId, memoryId."),
            new KeyValuePair<string, string>(
                "memory_upsert",
                "Save a memory. Use it after a decision, a discovered fact, a user preference or correction, or a task with a lesson. Idempotent on (scope, category, slug): reuse a slug to update. Required: scopeId, category (a name, created if new, or a cat_ id), slug, body. Optional: title (specific), summary (one line), type, links, supersedes. Never store secrets. "
                + "When the new memory replaces an older one (a changed decision, a corrected fact), pass the old slug in supersedes: search then ranks the old memory after the new one and marks it outdated. "
                + "The response lists existing memories that closely resemble this one in similarMemories; if one of them says the same thing, update it (reuse its slug) instead of keeping both, or supersede it. "
                + "Each time you save, you may also audit the scope: add or update categories that are missing or unclear, and fill in, correct, or remove memories until the scope is complete."),
            new KeyValuePair<string, string>(
                "memory_search",
                "Search memory. Use it before answering a question about the project and before changing code or making a decision, with a short natural-language query; prefer what memory says over assumptions and cite the slugs you use. Required: scopeId (from session_start), queryText. Optional: mode (Keyword|Semantic|Hybrid), topK, categoryName, minScore, recencyWeight, superseded, linkExpansion, diversity, rerank, minRerankScore, additionalQueries, additionalQueryWeight, subQueries, decompose, expand, expansionWeight. For a question about several distinct things, pass each part in additionalQueries (or set decompose) so every part's memories are found. "
                + "Hits replaced by a newer memory carry supersededBy (prefer the replacement); hits added by following links carry linkedFrom."),
            new KeyValuePair<string, string>(
                "memory_delete",
                "Delete a memory by id. Required: scopeId, memoryId."),
            new KeyValuePair<string, string>(
                "scope_read",
                "Read a single scope by id. Required: scopeId."),
            new KeyValuePair<string, string>(
                "scope_update",
                "Update a scope's name, description, filesystem mirror, models, or retrieval settings (store provider and dimensionality are immutable); settings not passed are kept. Required: scopeId. Optional: name, description, filesystemMirror and targetPath (RecallDb scopes: also write every memory to an Open Knowledge Format bundle at targetPath; turning it on copies existing memories there), rerankEndpointId (empty string removes it), rerankCandidates, rerankMinScore, inferenceEndpointId and queryEndpointId (the models for chat and for query rewriting/expansion), conversationRewrite, queryExpansion (Off|On|Auto), queryDecomposition."),
            new KeyValuePair<string, string>(
                "scope_delete",
                "Delete a scope and cascade its categories, memories, and scope instructions. Required: scopeId."),
            new KeyValuePair<string, string>(
                "category_read",
                "Read a single category by id. Required: scopeId, categoryId."),
            new KeyValuePair<string, string>(
                "category_update",
                "Update a category. Required: scopeId, categoryId, name. Optional: description, instructions."),
            new KeyValuePair<string, string>(
                "category_delete",
                "Delete a category. Required: scopeId, categoryId."),
            new KeyValuePair<string, string>(
                "endpoint_read",
                "Read a single model endpoint by id. Required: endpointId."),
            new KeyValuePair<string, string>(
                "endpoint_create",
                "Create a model endpoint (embedding or inference). Required: name, baseUrl. Optional: kind, apiFormat, authType + auth fields, model, dimensionality, healthCheckUrl, reasoning, active. Requires tenant administration."),
            new KeyValuePair<string, string>(
                "endpoint_update",
                "Update a model endpoint. Required: endpointId, name, baseUrl. Optional: kind, apiFormat, authType + auth fields, model, dimensionality, healthCheckUrl, reasoning, active. Requires tenant administration."),
            new KeyValuePair<string, string>(
                "endpoint_delete",
                "Delete a model endpoint. Required: endpointId. Requires tenant administration."),
            new KeyValuePair<string, string>(
                "endpoint_health",
                "Probe and return the health of the tenant's model endpoints."),
            new KeyValuePair<string, string>(
                "chat",
                "Ask a question answered from a scope's memory (retrieval-augmented). Required: scopeId, question. Optional: topK (default 8), inferenceEndpointId, history. For a follow-up question, pass the earlier messages in history so the question is understood in context. Returns the answer plus cited memory ids."),
            new KeyValuePair<string, string>(
                "collection_enumerate",
                "List the RecallDB collections backing this tenant's scopes."),
            new KeyValuePair<string, string>(
                "collection_read",
                "Read a single RecallDB collection by id. Required: collectionId."),
            new KeyValuePair<string, string>(
                "collection_create",
                "Create a RecallDB collection directly. Required: name, dimensionality. Optional: description. (Normally scopes provision their own collection.)"),
            new KeyValuePair<string, string>(
                "collection_delete",
                "Delete a RecallDB collection by id. Required: collectionId."),
            new KeyValuePair<string, string>(
                "instruction_create",
                "Create an instruction. Required: name, content. Optional: scopeId (omit for a tenant-global instruction), mergeMode (Append|Replace|Hide, for scope instructions), position, active. Requires tenant administration."),
            new KeyValuePair<string, string>(
                "instruction_update",
                "Update an instruction by id (tenant-global or scope-specific; the scope binding is preserved). Required: instructionId, name, content. Optional: mergeMode, position, active. Requires tenant administration."),
            new KeyValuePair<string, string>(
                "instruction_delete",
                "Delete an instruction by id. Required: instructionId. Requires tenant administration.")
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// The default description of a tool.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <returns>The default description, or null for an unknown tool.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
        public static string? DefaultDescription(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            foreach (KeyValuePair<string, string> entry in Defaults)
            {
                if (string.Equals(entry.Key, name, StringComparison.Ordinal)) return entry.Value;
            }

            return null;
        }

        #endregion
    }
}
