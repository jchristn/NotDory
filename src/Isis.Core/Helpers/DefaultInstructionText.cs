namespace Isis.Core.Helpers
{
    /// <summary>
    /// Text of the default tenant instructions that describe how an agent starts working, shared by the seeder (new
    /// tenants) and the migration that updates unedited copies in existing tenants.
    /// </summary>
    public static class DefaultInstructionText
    {
        #region Public-Members

        /// <summary>
        /// The "Start here" instruction.
        /// </summary>
        public const string StartHere =
@"This is this tenant's standing guidance for memory in Isis. session_start returns it, merged with your scope's own
instructions, every time you begin work. The tenant maintains it and may edit it.

Naming: the product is 'Isis' (a proper noun, written 'Isis' or 'isis'). It is NOT an acronym: never write it as the
all-caps 'ISIS', which refers to something else entirely.

Start every session with session_start(project), passing the repository or project name. It returns your scope
(created if new), its categories and their instructions, these instructions, and the most recent memories: read them
before planning. No tool needs a tenantId; your credential identifies the tenant. Use the returned scopeId on every
memory tool.";

        /// <summary>
        /// The "Tools" instruction.
        /// </summary>
        public const string Tools =
@"Quick reference; your MCP client also receives each tool's full input schema.
- session_start(project, [createIfMissing, maxMemories]): start here. Your scope, its categories and instructions,
  and the most recent memories.
- memory_search(scopeId, queryText, [mode=Keyword|Semantic|Hybrid, topK, categoryName]): before answering a
  question about the project and before changing code or making a decision.
- memory_upsert(scopeId, category, slug, body, [title, summary, type, links, supersedes]): after a decision, a
  discovered fact, a preference, or a correction. category is a name (created if new) or a cat_ id; idempotent on
  (scope, category, slug), so reuse a slug to update. type is User, Feedback, Project, or Reference.
- memory_read, memory_enumerate, memory_delete; chat(scopeId, question) for a cited answer from memory.
- scope_enumerate, scope_create, category_enumerate, category_create; guide and instructions re-read the context
  session_start returned.
- endpoint_enumerate: list model endpoints.";

        #endregion
    }
}
