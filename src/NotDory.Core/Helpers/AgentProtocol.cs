namespace NotDory.Core.Helpers
{
    using System;

    /// <summary>
    /// The standing operating procedure NotDory gives an agent: sent as the MCP server instructions on connect (which agent
    /// harnesses place in the model's system prompt) and at the top of every session start, so memory use is part of how
    /// the agent works from its first turn rather than something it has to discover.
    /// </summary>
    public static class AgentProtocol
    {
        #region Public-Members

        /// <summary>
        /// The MCP server instructions: short, imperative, and the same for every caller.
        /// </summary>
        public const string ServerInstructions =
            "NotDory is your persistent memory for this work: facts, decisions, conventions, and preferences saved by you and "
            + "other agents across sessions. Use it as part of how you work, not as an optional extra.\n"
            + "1. Start: call session_start once, with project set to the git repository name (from the origin remote) or, "
            + "outside a repository, the project or working directory name. If a session-start hook already gave you this "
            + "context, use it instead. It returns your scope, its categories and instructions, and the most recent "
            + "memories. Read them before planning. No tool needs a tenantId.\n"
            + "2. Before answering a question about the project, and before changing code or making a decision, call "
            + "memory_search with a short natural-language query. Prefer what memory says over assumptions, and cite the "
            + "slugs of memories you rely on.\n"
            + "3. Save as you go: after a decision, a discovered fact, a user preference or correction, or a task with a "
            + "lesson, call memory_upsert (name the category; a new name creates it). Reuse a slug to update a memory, and "
            + "pass supersedes when a fact changes. One fact per memory, with a specific title and a one-line summary.\n"
            + "4. Audit when you save: every time you write memories, you have license to audit this scope and fix what you "
            + "find, without asking first. Check the categories (category_enumerate): if the kinds of knowledge this project "
            + "has lack a category, or one is too broad or poorly described, create or update it. Then check that memory "
            + "is complete (memory_enumerate, memory_search): could a new agent start work from it alone? If architecture, "
            + "conventions, build and test steps, decisions, or open work are missing, thin, stale, or duplicated, add, "
            + "update, supersede, or delete memories until they are complete and correct.\n"
            + "5. Never store secrets, credentials, or personal data.";

        #endregion

        #region Public-Methods

        /// <summary>
        /// The protocol for a session in one scope: the server instructions plus the scope to use.
        /// </summary>
        /// <param name="scopeId">The session's scope id.</param>
        /// <param name="scopeName">The session's scope name.</param>
        /// <param name="instructions">The server instructions in effect (an administrator's edit); null uses <see cref="ServerInstructions"/>.</param>
        /// <returns>The protocol text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when scopeId or scopeName is null.</exception>
        public static string ForScope(string scopeId, string scopeName, string? instructions = null)
        {
            if (scopeId == null) throw new ArgumentNullException(nameof(scopeId));
            if (scopeName == null) throw new ArgumentNullException(nameof(scopeName));
            return "This session's memory is the scope '" + scopeName + "' (scopeId " + scopeId + "); pass that scopeId to "
                + "memory_search, memory_upsert, memory_read, and chat.\n" + (string.IsNullOrWhiteSpace(instructions) ? ServerInstructions : instructions);
        }

        #endregion
    }
}
