> **This document is meant to be pasted into Claude Code's system prompt or CLAUDE.md.** It gives Claude everything it needs to connect to and use the NotDory agent-memory platform. Copy the contents below into your Claude Code configuration.

---

## Connecting Claude Code to NotDory over MCP

NotDory serves the modern **MCP Streamable HTTP + SSE** transport at `http://127.0.0.1:8720/mcp`. Every request authenticates with a single header: `x-access-key` (a credential access key, default `notdorydefaultkey`). The access key alone identifies a tenant credential and scopes the connection to its tenant. The secret key is never sent by Claude Code. Because the access key authenticates on its own, treat it as a **capability token** and prefer a least-privilege credential. Change the default before exposing NotDory outside a trusted local environment.

### Option A -- `notdory mcp install` (one-step, recommended)

The fastest way to connect Claude Code is the built-in installer:

```bash
notdory mcp install
```

This patches `~/.claude.json` with a `notdory` MCP server pointing at `http://127.0.0.1:8720/mcp`, including the `x-access-key` header. It reads the port and host from `notdory.mcp.json` and the `NOTDORY_MCP_*` environment variables, and accepts optional `--access-key`, `--port`, and `--host` flags. It is safe to run repeatedly -- it updates the existing `notdory` entry in place and preserves every other MCP server. Restart Claude Code afterward to pick up the change.

### Option B -- `claude mcp add` (CLI)

Add NotDory as an HTTP MCP server with the access-key header inline:

```bash
claude mcp add --transport http notdory http://127.0.0.1:8720/mcp --header "x-access-key: notdorydefaultkey"
```

The access key is the only header required; the secret key is never sent. Restart Claude Code (or reload the MCP servers) after adding.

### Option C -- Project `.mcp.json`

Commit a `.mcp.json` file at the root of your project so every agent working in that repo shares the same NotDory connection:

```json
{
  "mcpServers": {
    "notdory": {
      "type": "http",
      "url": "http://127.0.0.1:8720/mcp",
      "headers": {
        "x-access-key": "notdorydefaultkey"
      }
    }
  }
}
```

Only the access-key header is required; the secret key is never sent. Because the access key is a capability token, do not commit production credentials into a shared repo; prefer a least-privilege credential, or keep the file untracked. Once connected, `tools/list` returns the 32 NotDory tools.

---

# NotDory Memory Instructions

You have access to the NotDory agent-memory platform via MCP tools. NotDory is **not** an orchestrator -- it is durable, shared **memory**. Use it to recall what you (or another agent) learned before, and to record durable facts so the next session does not start from zero. NotDory is memory, not a filesystem: read before you write, and prefer summaries before full bodies to conserve tokens.

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

The store behind a scope determines search power: `RecallDb` supports `Semantic` and `Hybrid` search (vector plus full-text, with optional cross-encoder reranking when the scope has a rerank endpoint); `Filesystem` is `Keyword`-only. A `RecallDb` scope with `filesystemMirror` on searches like any RecallDb scope and also keeps an Open Knowledge Format copy of every memory in a `.okf` directory under its `targetPath` (the repository root).

## Core Workflow

Every memory session follows this pattern: **Discover -> Recall -> Record -> Curate**

### 1. Discover

Start every session with one call, before planning anything:

```
session_start({ project: "<repository or project name>", path: "<absolute path of the repository root>" })
  -> your scope for the project (created if new), how to use NotDory, the scope's categories
     and their instructions, the tenant's standing instructions, and the most recent memories
```

It replaces the old whoami, instructions, scope_enumerate, and guide sequence. No tool needs a `tenantId`: your
credential identifies your tenant (pass one only to work in another tenant you can access). Use the returned `scopeId`
on every memory tool. `path` lets a new scope mirror its memories to an Open Knowledge Format bundle in
`<path>/.okf`, which you can commit with the code; the `notice` says when the NotDory server cannot see that directory.
With no `project` and several scopes, the response lists them so you can pick one. Read the
categories' **instructions** and the recent memories before writing anything: they are the contract for what belongs
where.

**One scope per project.** Every project you do a meaningful amount of work on gets its own scope; never put one
project's knowledge in another project's scope. `session_start` creates the scope when none matches the project name,
and its `notice` says so when the scope is new or empty. When the scope is new, empty, or thin, onboard the project
before or alongside your task:

1. **Examine the project.** Read the structure and key details: README and docs, build and package files, directory
   layout, entry points, tests, configuration, and conventions.
2. **Describe the scope.** `scope_update({ scopeId, description })` with what the project is.
3. **Create categories** for the kinds of knowledge the project has (for example `architecture`, `conventions`,
   `build-and-test`, `decisions`, `open-work`), each with a `description` and `instructions` saying what belongs in it:
   `category_create({ scopeId, name, description, instructions })`.
4. **Create memories** for what you found (`memory_upsert`), enough that a new agent could start work from memory
   alone.

### 2. Recall

Before you do work, check whether the answer is already remembered:

```
memory_search({ scopeId, queryText, mode: "Hybrid", topK: 5 })
memory_enumerate({ scopeId, category })   -> token-cheap summaries by category
memory_read({ scopeId, memoryId })        -> the full body of one memory
chat({ scopeId, question, history })      -> a synthesized, cited answer instead of a list of hits
```

Search first (`Hybrid` or `Semantic` on a RecallDb scope; `Keyword` works anywhere). For a question about several distinct things, pass each part in `additionalQueries` so every part's memories are found. A hit that carries `supersededBy` is outdated: prefer the memory it names. Enumerate when you want a list rather than a query. Only call `memory_read` for the specific memories you actually need -- it is the only tool that returns full bodies.

### 3. Record

When you learn something durable, write it:

```
memory_upsert({ scopeId, category, slug, title, summary, body, type, links, supersedes })
```

`memory_upsert` is **idempotent on `(scope, category, slug)`** -- re-writing the same slug updates the memory in place instead of duplicating it. Choose a stable, descriptive slug. Provide a crisp one-line `summary`; it is the recall hook shown in enumerate and search results. Put the slugs of related memories in `links`. When the new memory replaces an older one (a changed decision, a corrected fact), pass the old slug in `supersedes`: search then ranks the old memory after the new one and marks it outdated.

The upsert response can include `similarMemories`: existing memories that look like duplicates of the one you just wrote. If one says the same thing, reuse its slug (update it) instead of keeping both, or supersede it.

Outside onboarding, create a category only when no existing one fits:

```
category_create({ scopeId, name, description, instructions })
```

Always supply `instructions` so future agents know when and how to write into it. Create categories sparingly -- too many fragments dilutes recall.

### 4. Curate

Keep memory trustworthy. Delete what is proven wrong or obsolete rather than leaving stale guidance behind (when the old version is worth keeping for history, supersede it instead):

```
memory_delete({ scopeId, memoryId })
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
| `session_start` | `project`, `path`, `createIfMissing`, `maxMemories` | Start here, once per session: your scope for the project (created if new), how to use NotDory, the categories and instructions, and the most recent memories. |
| `whoami` | -- | Show your tenant and principal. Not needed to start: `session_start` returns the same. |
| `instructions` | `scopeId` | Re-read the tenant's standing memory manual (or a scope's effective instructions); `session_start` already returns them. |
| `scope_enumerate` | -- | List the memory scopes in a tenant. |
| `scope_create` | `name` (required); `description`, `storeProvider`, `embeddingEndpointId`, `dimensionality`, `filesystemLayout`, `targetPath` | Create a scope when none fits (typically once per project). |
| `endpoint_enumerate` | `kind` | List model endpoints (`Embedding`/`Inference`; rerankers are inference endpoints) -- e.g. to choose an `embeddingEndpointId`, or to confirm whether semantic (RecallDb) scopes are possible. |
| `guide` | `scopeId` (required) | The scope's categories, their usage instructions, and store capabilities. Call before writing. |
| `category_enumerate` | `scopeId` (required) | List categories in a scope, including usage instructions. |
| `category_create` | `scopeId`, `name` (required); `description`, `instructions` | Create a category. Supply `instructions`. |
| `memory_enumerate` | `scopeId` (required); `category`, `maxResults` | List token-cheap memory summaries (no bodies). `category` filters by category **id**. |
| `memory_read` | `scopeId`, `memoryId` (required) | Read one memory's full body. |
| `memory_upsert` | `scopeId`, `category` (a name, created if new, or a `cat_` id), `slug`, `body` (required); `title`, `summary`, `type`, `links`, `supersedes` | Create or update a memory. Idempotent on `(scope, category, slug)`. Pass `supersedes` (old slugs) when this memory replaces older ones. The response may list `similarMemories`. |
| `memory_search` | `scopeId`, `queryText` (required); `mode`, `topK`, `categoryName`, `superseded`, `additionalQueries`, `rerank` | Search a scope. `mode` = `Keyword`/`Semantic`/`Hybrid`. `categoryName` filters by category name (or `cat_` id). `superseded` = `Demote` (default)/`Hide`/`Include`. `additionalQueries` holds up to 4 extra queries (the parts of a multi-part question). `rerank` defaults to on when the scope has a rerank endpoint. More tuning options are in `docs/MCP_API.md`. |
| `memory_delete` | `scopeId`, `memoryId` (required) | Delete a memory by id. |
| `chat` | `scopeId`, `question` (required); `topK`, `inferenceEndpointId`, `history` | Ask a question answered from the scope's memory; returns the answer plus cited memory ids. `history` holds the earlier `{ role, content }` messages so a follow-up question is understood in context. |

`type` on upsert is one of `User`, `Feedback`, `Project`, `Reference`. `Semantic` and `Hybrid` search require a RecallDb-backed scope; `Keyword` works on any store.

The server exposes 33 tools in all. The rest are management tools: `scope_read`/`scope_update`/`scope_delete`, `category_read`/`category_update`/`category_delete`, `endpoint_read`/`endpoint_create`/`endpoint_update`/`endpoint_delete`/`endpoint_health`, `collection_enumerate`/`collection_read`/`collection_create`/`collection_delete`, and `instruction_create`/`instruction_update`/`instruction_delete`. Endpoint and instruction writes require tenant administration. See `docs/MCP_API.md` for every tool's arguments.

## Decision-Making Guidance

- **Always start with `session_start`** (project = the repository or project name) and keep its `scopeId`. Its categories, instructions, and recent memories are your context for the session.
- **One scope per project.** If you do a meaningful amount of work on a project, it gets its own scope. When that scope is new, empty, or thin, onboard it: examine the project, describe the scope, create categories with instructions, and save memories.
- **Read the instructions and categories `session_start` returns before writing.** Category `instructions` and the tenant's standing instructions are the contract; honor them.
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

When a call fails, `success` is `false`, `statusCode` carries the upstream code (e.g. `401`, `403`, `404`), and `data` holds the error body. A request with no access key is rejected before any tool runs with `401`. A `403` on a tenant call means your credential is not authorized for that `tenantId` -- omit `tenantId` so your credential's own tenant is used.
