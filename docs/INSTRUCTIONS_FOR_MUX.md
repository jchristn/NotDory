> **This document is meant to be provided to Mux as system context and paired with an MCP config that points at Isis.** It gives Mux everything it needs to connect to and use the Isis agent-memory platform. Copy the contents below into your Mux system prompt or skill/context files.

---

## Connecting Mux to Isis over MCP

Mux talks to Isis through Isis's MCP server. Isis serves the modern **MCP Streamable HTTP + SSE** transport at `http://127.0.0.1:8720/mcp`. Mux can send only **one** auth header, so it authenticates with the credential **access key carried as a bearer token** (`Authorization: Bearer <accessKey>`, default access key `isisdefaultkey`). The access key identifies a tenant credential and scopes the connection to its tenant. The **secret key is never sent to Mux** and never leaves your machine — because the access key alone authenticates, it is a **capability token**: use a least-privilege credential and change the default before exposing Isis outside a trusted local environment.

### Option A -- Interactive (`/mcp` in a Mux session)

1. Start Mux interactively:

```bash
mux
```

2. In the Mux session, run the MCP servers manager:

```
/mcp
```

   (aliases: `/mcp-servers`, `/servers`; also on the `F1` menu under **Model**.)

3. Choose **+ Add MCP server...** and fill in the guided form:
   - **name**: `isis`
   - **transport**: `http`
   - **url**: `http://127.0.0.1:8720`
   - **mcp path**: `/mcp` (the default -- leave as-is)
   - **auth**: choose **bearer** and set the bearer token to your credential's **access key** (default `isisdefaultkey`). Do **not** add `x-access-key` / `x-secret-key` headers — Mux sends the access key as the bearer token and never sends the secret.

Each server row shows a live connectivity glyph -- `●` online (with its discovered tool count) or `○` offline. Once `isis` shows `●` with a nonzero tool count, you are connected. The server is saved to the Mux config directory's `mcp-servers.json`, so it loads automatically in future sessions.

### Option B -- Config file (headless / scripted runs)

Mux's headless MCP is off unless you pass `--mcp-config`. Mux persists servers in its `mcp-servers.json`; a server entry lives in the `servers` array with a `bearer` auth block. Create a Mux MCP config file, for example `isis.mcp.json`:

```json
{
  "servers": [
    {
      "name": "isis",
      "transport": "http",
      "url": "http://127.0.0.1:8720",
      "mcpPath": "/mcp",
      "auth": { "type": "bearer", "bearerToken": "isisdefaultkey" }
    }
  ]
}
```

`bearerToken` is your credential's **access key**. There is no `headers` object and no secret key — Mux
cannot send a second header, so the access key alone authenticates. Run Mux with that config so the Isis
tools load:

```bash
mux --mcp-config ./isis.mcp.json print --yolo "what do we remember about this project?"
```

Or pass the config inline:

```bash
mux print --yolo --mcp-config '{"servers":[{"name":"isis","transport":"http","url":"http://127.0.0.1:8720","mcpPath":"/mcp","auth":{"type":"bearer","bearerToken":"isisdefaultkey"}}]}' "what do we remember about this project?"
```

The access key is the only credential Mux sends; it is public and transferable, so treat it as a capability token and scope it least-privilege. The secret key is never sent to Mux.

### Notes

Verify the connection with `mux probe --output-format json --require-tools` -- the `isis` server should appear with a nonzero tool count. Once connected, `tools/list` returns the 32 Isis tools (the MCP `ping` method is answered too, but it is not a tool). If the server shows `○` offline, confirm Isis is running and listening on `127.0.0.1:8720` (see Troubleshooting in `CONNECTING_AGENTS.md`).

---

# Isis Memory Instructions

You have access to the Isis agent-memory platform via MCP tools. Isis is **not** an orchestrator -- it is durable, shared **memory**. Use it to recall what you (or another agent) learned before, and to record durable facts so the next session does not start from zero. Isis is memory, not a filesystem: read before you write, and prefer summaries before full bodies to conserve tokens.

## Concepts

| Term | What it is | ID prefix |
|------|-----------|-----------|
| **Tenant** | An isolated memory account; everything you read and write lives under one tenant | `ten_` |
| **Scope** | A memory space -- a project, a book, or a shared "global" space -- backed by a store (RecallDb or Filesystem) | `scp_` |
| **Category** | A labeled bucket within a scope that carries usage instructions for what to write and how | `cat_` |
| **Memory** | One atomic note: `slug`, `title`, `body`, `summary`, `tags`, `links` | `mem_` |
| **Credential** | The access key your connection authenticates with; maps to a tenant | `crd_` |

**Tenant instructions** are the tenant's standing "memory manual" -- always-on guidance the operator wants every agent to honor. Read them with the `instructions` tool early in a session; they apply across every scope in the tenant. Pass a `scopeId` to get the scope's effective instructions (the tenant-wide set merged with the scope's own).

**Chat-with-Memory** is retrieval-augmented reasoning over a whole scope, available as the `chat` tool: it uses the same retrieval as `memory_search`, then adds an inference step to synthesize a grounded answer that cites the memory ids it used. Chat keeps no conversation state, so for a follow-up question pass the earlier messages in `history` (oldest first, each `{ role, content }` with `role` = `user` or `assistant`); the server rewrites the follow-up into a standalone question before searching.

The store behind a scope determines search power: `RecallDb` supports `Semantic` and `Hybrid` search (vector plus full-text, with optional cross-encoder reranking when the scope has a rerank endpoint); `Filesystem` is `Keyword`-only. `Verbex` is listed as a store provider but is not wired yet (its searches fail), so do not create Verbex scopes.

## Core Workflow

Every memory session follows this pattern: **Discover -> Recall -> Record -> Curate**

### 1. Discover

Learn who you are and what memory exists before doing anything else:

```
whoami()                                   -> your tenantId (cache it for the session)
instructions({ tenantId })                 -> the tenant's standing memory manual (read early; re-read if it changes)
scope_enumerate({ tenantId })              -> the scopes in your tenant (create one with scope_create if none fits)
guide({ tenantId, scopeId })               -> the scope's categories, their usage instructions, and store capabilities
```

Call `instructions` right after `whoami` to load the tenant's standing guidance. Then `guide` is the single most important per-scope call: it returns each category's **instructions** -- the contract for when and how to write that kind of memory -- alongside the store's search **capabilities**. Read both before writing anything.

### 2. Recall

Before you do work, check whether the answer is already remembered:

```
memory_search({ tenantId, scopeId, queryText, mode: "Hybrid", topK: 5 })
memory_enumerate({ tenantId, scopeId, category })   -> token-cheap summaries by category
memory_read({ tenantId, scopeId, memoryId })        -> the full body of one memory
chat({ tenantId, scopeId, question, history })      -> a synthesized, cited answer instead of a list of hits
```

Search first (`Hybrid` or `Semantic` on a RecallDb scope; `Keyword` works anywhere). For a question about several distinct things, pass each part in `additionalQueries` so every part's memories are found. A hit that carries `supersededBy` is outdated: prefer the memory it names. Enumerate when you want a list rather than a query. Only call `memory_read` for the specific memories you actually need -- it is the only tool that returns full bodies.

### 3. Record

When you learn something durable, write it:

```
memory_upsert({ tenantId, scopeId, categoryId, slug, title, summary, body, type, links, supersedes })
```

`memory_upsert` is **idempotent on `(scope, category, slug)`** -- re-writing the same slug updates the memory in place instead of duplicating it. Choose a stable, descriptive slug. Provide a crisp one-line `summary`; it is the recall hook shown in enumerate and search results. Put the slugs of related memories in `links`. When the new memory replaces an older one (a changed decision, a corrected fact), pass the old slug in `supersedes`: search then ranks the old memory after the new one and marks it outdated.

The upsert response can include `similarMemories`: existing memories that look like duplicates of the one you just wrote. If one says the same thing, reuse its slug (update it) instead of keeping both, or supersede it.

Create a category only when no existing one fits:

```
category_create({ tenantId, scopeId, name, description, instructions })
```

Always supply `instructions` so future agents know when and how to write into it. Create categories sparingly -- too many fragments dilutes recall.

### 4. Curate

Keep memory trustworthy. Delete what is proven wrong or obsolete rather than leaving stale guidance behind (when the old version is worth keeping for history, supersede it instead):

```
memory_delete({ tenantId, scopeId, memoryId })
```

## When to Write a Memory

Write a memory when you learn something a future session would otherwise have to rediscover:

- **Durable facts about the project** -- where a subsystem lives, how to build/test/run, a non-obvious convention, an architectural decision and its rationale.
- **User preferences and feedback** -- how the user wants things done, corrections they made, standing instructions.
- **Reference material** -- API shapes, config keys, external dependencies and their quirks.
- **Outcomes worth remembering** -- what a fix was, why an approach failed, what to try next.

Do **not** write:

- Transient state that will be false next hour (open file, current cursor position).
- Anything the `guide` categories tell you does not belong.
- **Secrets, credentials, tokens, or raw sensitive data.** Never store these as memory content.

Match every write to a category and follow that category's `instructions`. When in doubt, search first -- if a near-duplicate exists, update it by re-using its slug instead of creating a second copy.

## Tool Reference

| Tool | Parameters | Description |
|------|-----------|-------------|
| `whoami` | -- | Resolve the tenant and principal your credential maps to. Call first; its response carries the `tenantId` every other tool needs. |
| `instructions` | `tenantId` (required); `scopeId` | Read the tenant's standing memory manual (or a scope's effective instructions). Call right after `whoami`. |
| `scope_enumerate` | `tenantId` (required) | List the memory scopes in a tenant. |
| `scope_create` | `tenantId`, `name` (required); `description`, `storeProvider`, `embeddingEndpointId`, `dimensionality`, `filesystemLayout`, `targetPath` | Create a scope when none fits (typically once per project). |
| `endpoint_enumerate` | `tenantId` (required); `kind` | List model endpoints (`Embedding`/`Inference`; rerankers are inference endpoints) -- e.g. to choose an `embeddingEndpointId`, or to confirm whether semantic (RecallDb) scopes are possible. |
| `guide` | `tenantId`, `scopeId` (required) | The scope's categories, their usage instructions, and store capabilities. Call before writing. |
| `category_enumerate` | `tenantId`, `scopeId` (required) | List categories in a scope, including usage instructions. |
| `category_create` | `tenantId`, `scopeId`, `name` (required); `description`, `instructions` | Create a category. Supply `instructions`. |
| `memory_enumerate` | `tenantId`, `scopeId` (required); `category`, `maxResults` | List token-cheap memory summaries (no bodies). `category` filters by category **id**. |
| `memory_read` | `tenantId`, `scopeId`, `memoryId` (required) | Read one memory's full body. |
| `memory_upsert` | `tenantId`, `scopeId`, `categoryId`, `slug`, `body` (required); `title`, `summary`, `type`, `links`, `supersedes` | Create or update a memory. Idempotent on `(scope, category, slug)`. Pass `supersedes` (old slugs) when this memory replaces older ones. The response may list `similarMemories`. |
| `memory_search` | `tenantId`, `scopeId`, `queryText` (required); `mode`, `topK`, `categoryName`, `superseded`, `additionalQueries`, `rerank` | Search a scope. `mode` = `Keyword`/`Semantic`/`Hybrid`. `categoryName` filters by category name (or `cat_` id). `superseded` = `Demote` (default)/`Hide`/`Include`. `additionalQueries` holds up to 4 extra queries (the parts of a multi-part question). `rerank` defaults to on when the scope has a rerank endpoint. More tuning options are in `docs/MCP_API.md`. |
| `memory_delete` | `tenantId`, `scopeId`, `memoryId` (required) | Delete a memory by id. |
| `chat` | `tenantId`, `scopeId`, `question` (required); `topK`, `inferenceEndpointId`, `history` | Ask a question answered from the scope's memory; returns the answer plus cited memory ids. `history` holds the earlier `{ role, content }` messages so a follow-up question is understood in context. |

`type` on upsert is one of `User`, `Feedback`, `Project`, `Reference`. `Semantic` and `Hybrid` search require a RecallDb-backed scope; `Keyword` works on any store.

The server exposes 32 tools in all. The rest are management tools: `scope_read`/`scope_update`/`scope_delete`, `category_read`/`category_update`/`category_delete`, `endpoint_read`/`endpoint_create`/`endpoint_update`/`endpoint_delete`/`endpoint_health`, `collection_enumerate`/`collection_read`/`collection_create`/`collection_delete`, and `instruction_create`/`instruction_update`/`instruction_delete`. Endpoint and instruction writes require tenant administration. See `docs/MCP_API.md` for every tool's arguments.

## Decision-Making Guidance

- **Always start with `whoami`** and cache the `tenantId` -- nearly every other tool requires it. `scopeId` comes from `scope_enumerate` (or `scope_create` when none fits).
- **Read the tenant `instructions` and the scope `guide` before writing.** Category `instructions` and the tenant's standing instructions are the contract; honor them.
- **Prefer summaries to bodies.** Enumerate and search return token-cheap summaries; only `memory_read` pulls a full body. Pull bodies deliberately.
- **Search before you write** to avoid creating a duplicate under a new slug. If a memory exists, update it by re-using its slug. If an upsert returns `similarMemories`, resolve the duplicate.
- **Supersede instead of contradicting.** When a fact changes, write the new memory with `supersedes` naming the old slug so search prefers the current one.
- **Keep slugs stable and descriptive** so repeated writes converge on one memory instead of scattering.
- **Curate as you go.** A wrong memory is worse than a missing one -- delete or overwrite stale guidance.

## Response Envelope

Every tool returns the same envelope; the proxied REST response is under `data`:

```json
{ "tool": "whoami", "success": true, "statusCode": 200, "data": { "tenantId": "ten_a1b2c3", "principalType": "Credential", "principalName": "default", "credentialId": "crd_9x8y7z" } }
```

When a call fails, `success` is `false`, `statusCode` carries the upstream code (e.g. `401`, `403`, `404`), and `data` holds the error body. A request with no access key is rejected before any tool runs with `401`. A `403` on a tenant call means your credential is not authorized for that `tenantId` -- call `whoami` and use the `tenantId` it returns.
