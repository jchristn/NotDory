namespace NotDory.Server.Services
{
    using System.Collections.Generic;
    using NotDory.Core.Helpers;
    using NotDory.Core.Models;

    /// <summary>
    /// The default instruction set (the agent "tool manual") provisioned for every tenant — the first-run
    /// default tenant and any tenant created through the API. These records are tenant-owned and fully
    /// editable in the dashboard (Memory → Instructions); tenants are expected to customise the category and
    /// memory guidance for their domain. They are surfaced to agents over MCP via <c>instructions</c>,
    /// in ascending position order.
    /// </summary>
    public static class DefaultInstructions
    {
        #region Public-Methods

        /// <summary>
        /// Build the default instruction records (tool manual) for a tenant, in ascending position order.
        /// </summary>
        /// <param name="tenantId">The owning tenant identifier.</param>
        /// <returns>The default instructions.</returns>
        public static List<Instruction> For(string tenantId)
        {
            return new List<Instruction>
            {
                new Instruction
                {
                    TenantId = tenantId,
                    Name = "Start here",
                    Position = 0,
                    Content =
DefaultInstructionText.StartHere
                },
                new Instruction
                {
                    TenantId = tenantId,
                    Name = "Tools",
                    Position = 1,
                    Content =
DefaultInstructionText.Tools
                },
                new Instruction
                {
                    TenantId = tenantId,
                    Name = "Memory model",
                    Position = 2,
                    Content =
@"Memory is organised as scopes → categories → memories.
- Scope: a memory space for one project/book/domain, bound to a store: RecallDB (semantic + keyword — needs an
  embedding endpoint; its dimension is fixed at creation to match the embedding model) or Filesystem (keyword,
  git-trackable files). Prefer RecallDB when an embedding endpoint exists (check with
  endpoint_enumerate); otherwise use Filesystem, which needs no embeddings.
- Category: a labelled bucket within a scope that carries its OWN usage instructions telling you when and how to
  write into it. Always read the category's instructions (session_start returns them) before writing.
- Memory: one atomic note — a stable slug, an optional title, a one-line summary (recall hook), the body, a
  type (User | Feedback | Project | Reference), and optional tags/links. Re-upserting the same slug updates in place."
                },
                new Instruction
                {
                    TenantId = tenantId,
                    Name = "Creating categories (customise for this domain)",
                    Position = 3,
                    Content =
@"Categories are how this tenant shapes what agents record. Edit this section to define the categories that fit
your domain, and give each a clear ""when to use me"" instruction (the instructions field on the category).

Guidance:
- Prefer a small set of durable categories over many ad-hoc ones. Create a category only when an existing one
  does not fit; check with category_enumerate first.
- Each category's instructions should say what belongs in it, the expected body shape, and what to leave out.

Example categories (replace with your own):
- decisions — architectural/product decisions and their rationale. Body: the decision, the alternatives, why.
- conventions — house rules and patterns to follow. Body: the rule, an example, the exception.
- glossary — domain terms and their meaning. Body: term → definition, with a canonical example."
                },
                new Instruction
                {
                    TenantId = tenantId,
                    Name = "Storing memories (customise for this domain)",
                    Position = 4,
                    Content =
@"Edit this section to specify how agents should write memories for your domain.

Defaults:
- One idea per memory. Do not bundle unrelated facts.
- Slugs are stable and descriptive (e.g. auth-token-rotation), lower-case-hyphenated; re-using a slug updates
  that memory rather than creating a duplicate.
- When a fact changes (a decision is reversed, a value or owner changes), update the existing memory: search for
  it, then upsert with the same slug and category. Do not add a second memory for the new value; the old one keeps
  surfacing in search and contradicts the new one. If the change is significant enough to keep history, say what
  it replaced and when in the body of the updated memory.
- Write a one-line summary that is a genuine recall hook — what you'd search for to find this later.
- Choose the category whose instructions match; if none fits, propose a new category rather than forcing it.
- Record durable facts, decisions, and conventions — not transient chatter or secrets."
                },
                new Instruction
                {
                    TenantId = tenantId,
                    Name = "Recall",
                    Position = 5,
                    Content =
@"Search before writing to avoid duplicates (and to find the memory to update when a fact changes), and search
before answering to ground your response.
- On RecallDB scopes prefer Semantic or Hybrid search for meaning-based recall; use Keyword for exact terms.
- Filesystem scopes are keyword-only and match literal terms.
- Narrow with the category filter when you know where the answer lives, and keep topK small."
                }
            };
        }

        #endregion
    }
}
