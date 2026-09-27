# Isis Documentation

Isis is an agent-memory platform: durable, structured, queryable memory that survives
across sessions, harnesses, and projects. Agents reach it over **MCP**; operators manage
it over **REST** and a dashboard.

## Documents

| Document | What it covers |
|----------|----------------|
| [CONNECTING_AGENTS.md](CONNECTING_AGENTS.md) | Connect an agent to Isis over MCP: endpoint, auth headers, ready-to-paste config for Claude Code / Cursor / Mux / generic MCP clients, the `isis mcp install` helper, a first-calls walkthrough, and troubleshooting. **Start here to connect an agent.** |
| [MCP_API.md](MCP_API.md) | Full reference for the 32 MCP tools: purpose, arguments, example requests and responses, the transport/endpoint, the response envelope, and the auth model. |
| [REST_API.md](REST_API.md) | Full reference for the REST API: authentication, tenants, users, credentials, the memory domain (scopes, categories, memories, search, chat, model endpoints), instructions, batch operations, request history, and server settings. |
| [INSTRUCTIONS_FOR_CLAUDE_CODE.md](INSTRUCTIONS_FOR_CLAUDE_CODE.md), [INSTRUCTIONS_FOR_CODEX.md](INSTRUCTIONS_FOR_CODEX.md), [INSTRUCTIONS_FOR_CURSOR.md](INSTRUCTIONS_FOR_CURSOR.md), [INSTRUCTIONS_FOR_GEMINI.md](INSTRUCTIONS_FOR_GEMINI.md), [INSTRUCTIONS_FOR_MUX.md](INSTRUCTIONS_FOR_MUX.md) | Per-agent guides to paste into an agent's system prompt or project rules: how to connect, plus the memory workflow and tool reference. |
| [../SEARCH_PIPELINE.md](../SEARCH_PIPELINE.md) | How retrieval works: hybrid vector and full-text search, fusion, reranking, multi-query search, and chat retrieval. |
| [ISIS_PLAN.md](ISIS_PLAN.md) | The original product plan: architecture, domain model, storage/search providers, REST and MCP surfaces, dashboard, deployment, and roadmap. Historical; the documents above describe the current state. |

## MCP at a Glance

- **Endpoint:** `http://127.0.0.1:8720/mcp` (streamable HTTP + SSE)
- **Auth:** the credential **access key**, sent as `Authorization: Bearer isisdefaultkey` or `x-access-key: isisdefaultkey`; the access key authenticates on its own (a capability token). Every agent sends the access key alone: Mux as a bearer token, Claude Code / Codex / Cursor / Gemini in the `x-access-key` header; none send a secret. The server still accepts an optional `x-secret-key` header and validates it only when present, but no installer sends one.
- **Core tools:** `whoami`, `instructions`, `scope_enumerate`, `scope_create`, `guide`,
  `category_enumerate`, `category_create`, `memory_enumerate`, `memory_read`,
  `memory_upsert`, `memory_search`, `memory_delete`, `chat`, plus management tools for
  scopes, categories, model endpoints, RecallDB collections, and instructions (32 in all)
- **Typical flow:** `whoami` -> `instructions` -> `scope_enumerate` -> `guide` ->
  `memory_search` / `memory_upsert` (or `chat` for a cited answer)

Connect Claude Code in one step with `isis mcp install`. See
[CONNECTING_AGENTS.md](CONNECTING_AGENTS.md) for every client.
