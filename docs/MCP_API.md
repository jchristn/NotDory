# NotDory MCP API

> **Naming.** The product is **NotDory** — a proper noun, written `NotDory` or `notdory`. It is **not**
> an acronym; never write it as the all-caps `NOTDORY`.
>
> **Tool names.** Tools are registered **without** a `notdory_` prefix (`whoami`, `memory_upsert`,
> `scope_create`, …). Your MCP client namespaces them under the server key you configure it with
> (conventionally `notdory`), so you will see them as e.g. `notdory.whoami` / `mcp__notdory__whoami` — a
> single, clean namespace, not a doubled `notdory_notdory_*`.
>
> **Start with `session_start`.** One call returns your scope for the project, how to use NotDory, the scope's
> categories and instructions, and the most recent memories. No tool requires a `tenantId`: it defaults to your
> credential's tenant (pass one only to work in another tenant your credential can access).

NotDory exposes an HTTP MCP server for AI agents. The MCP endpoint is:

```text
http://127.0.0.1:8720/mcp
```

The transport is **streamable HTTP + Server-Sent Events (SSE)**. The same server also
exposes the classic JSON-RPC path `/rpc` and the SSE events path `/events`; MCP clients
should use `/mcp`. The MCP host, port, and paths are configured in `notdory.mcp.json`
(`Hostname`, `Port`, `RpcPath`, `EventsPath`, `McpPath`) and can be overridden with the
`NOTDORY_MCP_HOSTNAME` and `NOTDORY_MCP_PORT` environment variables.

The NotDory MCP server is a thin, stateless front end: it authenticates the caller from the
transport headers and **proxies each tool call to the NotDory REST API** over loopback,
forwarding the caller's credentials so the REST server performs the authoritative
authentication and tenant scoping.

## Security Model

Every MCP request must present a credential **access key**. The access key alone
authenticates the caller and identifies the tenant credential; the secret key is **optional**.
Present the access key one of two ways:

| Auth material | How to send it | Default value | Notes |
|---------------|----------------|---------------|-------|
| Access key (bearer) | `Authorization: Bearer <accessKey>` | `notdorydefaultkey` | Single-header clients (e.g. Mux) use this |
| Access key (header) | `x-access-key: <accessKey>` | `notdorydefaultkey` | Equivalent alternative to the bearer token |
| Secret key (optional) | `x-secret-key: <secretKey>` | `notdorydefaultsecret` | Validated **only if present**; never required |

The MCP server accepts either the `Authorization: Bearer <accessKey>` token or the
`x-access-key` header, and honors an `x-secret-key` header only when one is supplied. A
request that presents **no access key** is rejected with HTTP `401` before any tool runs. The
access key is **public and transferable** — it authenticates on its own, so treat it as a
**capability token** and prefer a least-privilege credential. The `x-secret-key` header is a
legacy/optional extra: the server still validates it when present, but no agent installer
(Claude Code, Codex, Cursor, Gemini, Mux) sends one — the secret never leaves the client.

The access key (and the secret key, when present) is forwarded verbatim to the NotDory REST API,
which enforces tenant isolation. Tenant identity is never trusted from a tool argument alone;
the REST layer validates that the caller's credential is authorized for the `tenantId` it
operates on. Administrative power, when a credential's user has it, comes from the user
record's `IsAdmin` (system-wide) or `IsTenantAdmin` (tenant-wide) flags — there is no separate
admin key.

Change the default keys before exposing NotDory outside a trusted local environment.

## Response Envelope

Every tool returns the same structured envelope. The proxied REST response is embedded
under `data`.

| Field | Type | Description |
|-------|------|-------------|
| `tool` | string | The tool name, echoed back |
| `success` | boolean | `true` when the proxied REST call returned a 2xx status |
| `statusCode` | integer | The HTTP status code returned by the NotDory REST API |
| `data` | object, array, string, or null | The REST response body, parsed as JSON when possible |

```json
{
  "tool": "whoami",
  "success": true,
  "statusCode": 200,
  "data": {
    "tenantId": "ten_a1b2c3",
    "principalType": "Credential",
    "principalName": "default",
    "credentialId": "crd_9x8y7z"
  }
}
```

When the REST call fails, `success` is `false`, `statusCode` carries the upstream code
(for example `401`, `403`, `404`), and `data` contains the REST error body.

## On Connect

NotDory tells a connecting agent how to use it through two channels, both editable by a system administrator (dashboard
**Agent onboarding**, or `GET`/`PUT /v1.0/api/agent-protocol`):

- **Server instructions**, in the `initialize` result. Agent harnesses place them in the model's system prompt, so they
  reach the model even when the harness defers loading tool descriptions. The built-in text tells the agent to call
  `session_start` with the project name, search memory before answering or changing code, save decisions, facts,
  preferences, and corrections as it goes, and never store secrets. `session_start` repeats the same text with the
  session's scope id.
- **Tool descriptions**, in `tools/list`. Each says when to use the tool. An administrator can override any of them.

The MCP server re-reads both every `AgentProtocolRefreshSeconds` (default 30, in `notdory.mcp.json`); a changed tool
description re-registers the tools in their original order and sends connected clients `notifications/tools/list_changed`.
Tenant-specific guidance belongs in tenant or scope instructions, which `session_start` also returns.

## Tool Inventory

NotDory exposes **33** MCP tools at parity with the tenant-scoped REST surface. Each tool proxies the
REST route shown, forwarding the caller's credential; the write/CRUD tools accept the same fields as
the corresponding REST request body. `tools/list` returns only these tools; the MCP protocol `ping`
method is answered with an empty result (`{}`) on the handshake-era revisions. Like every other request it needs the
credential (a missing one gets 401) and, on the session transport, an initialized session. The stateless `2026-07-28`
revision removed `ping` (it gets `-32601`); probe connectivity with `GET /` instead.

| Tool | REST route proxied | Purpose |
|------|--------------------|---------|
| `session_start` | `POST /session` | Start here: the project's scope (created if new), protocol, categories, instructions, and recent memories |
| `whoami` | `GET /whoami` | Show the tenant and principal the caller's credential maps to |
| `instructions` | `GET .../instructions` or `.../scopes/{sid}/effective-instructions` | Standing instructions; pass `scopeId` for a scope's effective (merged) set |
| `guide` | `GET .../scopes/{sid}/guide` | A scope's categories, usage instructions, and store capabilities |
| `scope_enumerate` | `GET .../scopes` | List the memory scopes in a tenant |
| `scope_create` | `POST .../scopes` | Create a memory scope |
| `scope_read` | `GET .../scopes/{sid}` | Read a scope by id |
| `scope_update` | `GET` then `PUT .../scopes/{sid}` | Update a scope's name, description, models, or rerank and query settings, keeping everything else (an empty string clears a model or `queryExpansion`) |
| `scope_delete` | `DELETE .../scopes/{sid}` | Delete a scope (cascades categories, memories, scope instructions) |
| `category_enumerate` | `GET .../categories` | List categories in a scope |
| `category_create` | `POST .../categories` | Create a category |
| `category_read` | `GET .../categories/{cid}` | Read a category by id |
| `category_update` | `PUT .../categories/{cid}` | Update a category |
| `category_delete` | `DELETE .../categories/{cid}` | Delete a category |
| `memory_enumerate` | `GET .../memories` | List memory summaries in a scope (token-cheap) |
| `memory_read` | `GET .../memories/{mid}` | Read a single memory by id (full body) |
| `memory_upsert` | `POST .../memories` | Create or update a memory; idempotent on `(scope, category, slug)` |
| `memory_search` | `POST .../memories/search` | Search a scope's memory (keyword, semantic, or hybrid) |
| `memory_delete` | `DELETE .../memories/{mid}` | Delete a memory by id |
| `endpoint_enumerate` | `GET .../endpoints` | List a tenant's model endpoints |
| `endpoint_read` | `GET .../endpoints/{eid}` | Read a model endpoint by id |
| `endpoint_create` | `POST .../endpoints` | Create a model endpoint (base URL + auth) |
| `endpoint_update` | `PUT .../endpoints/{eid}` | Update a model endpoint |
| `endpoint_delete` | `DELETE .../endpoints/{eid}` | Delete a model endpoint |
| `endpoint_health` | `GET .../endpoint-health` | Probe and return endpoint health |
| `chat` | `POST .../scopes/{sid}/chat` | Ask a question answered from a scope's memory (RAG); returns answer + citations. Optional `history` (earlier `{ role, content }` messages, oldest first) lets a follow-up question be understood in context |
| `collection_enumerate` | `GET .../collections` | List the RecallDB collections backing scopes |
| `collection_read` | `GET .../collections/{cid}` | Read a RecallDB collection by id |
| `collection_create` | `POST .../collections` | Create a RecallDB collection directly |
| `collection_delete` | `DELETE .../collections/{cid}` | Delete a RecallDB collection |
| `instruction_create` | `POST .../instructions` or `.../scopes/{sid}/instructions` | Create a tenant-global or scope-specific instruction |
| `instruction_update` | `PUT .../instructions/{iid}` | Update an instruction by id |
| `instruction_delete` | `DELETE .../instructions/{iid}` | Delete an instruction by id |

Routes are shown relative to `/v1.0/api/tenants/{tenantId}` (except `session_start` and `whoami`). Management operations
(endpoint and instruction writes) require tenant administration; the REST server enforces this.
Deliberately **not** exposed over MCP (dashboard/REST-only): tenant, user, and credential management,
server settings, session/token login, and the raw request-history / operation-event feeds.

## Recommended Agent Workflow

NotDory is memory, not a filesystem. Read before you write, and prefer summaries before full
bodies to conserve tokens.

1. Call `session_start` with `project` set to the repository or project name. It returns your scope (created if
   new), the protocol, the categories and their instructions (the contract for what to write where), the scope's
   effective instructions, and the most recent memories. Read them before planning.
2. Keep the returned `scopeId` for every memory tool; no tool needs a `tenantId`.
3. Use `guide` or `instructions` only to re-read that context later in a long session.
4. Use `memory_search` to recall existing memory before doing work. Prefer `Hybrid`
   or `Semantic` mode on a RecallDB-backed scope; `Keyword` always works.
5. Use `memory_enumerate` to browse summaries by category when you want a list rather
   than a query, then `memory_read` to pull the full body of a specific memory.
6. When you learn something durable, write it with `memory_upsert`. Choose a stable
   `slug` so that re-writing the same fact updates it in place instead of duplicating.
7. Create a category with `category_create` only when no existing category fits and
   the guide's instructions do not already cover the content.
8. Use `memory_delete` to remove a memory that is wrong or obsolete.

## Common Arguments

Most tools take `scopeId`, which `session_start` returns. `tenantId` is optional on every tool and defaults to the
credential's tenant (resolved once per access key through `whoami` and cached). These are validated against the caller's credential
by the REST layer; a caller cannot act on a tenant its credential does not authorize.

## Tool Reference

### `session_start`

Start here, once per session. Resolves your tenant from the credential, finds the scope for the project (matching the
name ignoring case, spacing, and punctuation) or creates it, and returns everything needed to start working with memory.
It replaces the `whoami`, `instructions`, `scope_enumerate`, `guide` sequence.

Proxies `POST /v1.0/api/session`.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `project` | string | No | null | The git repository name if there is one, else the project or working directory name. Null picks the tenant's only scope when there is exactly one |
| `remote` | string | No | null | Git remote URL; its repository name is tried after `project` (stable across clones) |
| `directory` | string | No | null | Working directory name, tried last; it finds a scope but never creates one |
| `createIfMissing` | boolean | No | true | Create the project's scope (with the tenant's embedding endpoint) when none matches |
| `maxMemories` | integer | No | 15 | How many recent memories to include, 0 to 100 |

#### Response `data`

`tenantId`, `principal`, `scope` (`id`, `name`, `description`, `storeProvider`, `created`), `protocol` (the server
instructions with the scope id), `categories` (with their instructions), `instructions` (the scope's effective
instructions), `memoryCount`, `recentMemories` (`id`, `slug`, `category`, `title`, `summary`, `lastUpdateUtc`, newest
first), and, when no scope could be chosen, `scopes` and a `notice` explaining what to do.

### `whoami`

Show the tenant and principal the caller's credential maps to. Not needed to get started: `session_start` returns the
same, and no tool requires a `tenantId`.

Proxies `GET /v1.0/api/whoami`.

#### Input

No arguments.

#### Example Request

```json
{}
```

#### Response

```json
{
  "tool": "whoami",
  "success": true,
  "statusCode": 200,
  "data": {
    "tenantId": "ten_a1b2c3",
    "principalType": "Credential",
    "principalName": "default",
    "credentialId": "crd_9x8y7z"
  }
}
```

#### Guidance

- Callers authenticate with the credential access key (dev default `notdorydefaultkey`),
  presented as `Authorization: Bearer <accessKey>` or in the `x-access-key` header; an optional
  `x-secret-key` (dev default `notdorydefaultsecret`) is validated only when present. The caller
  resolves to the tenant credential the access key maps to. If that credential's user is an
  admin (`IsAdmin` / `IsTenantAdmin`), the resolved principal reflects it.

### `scope_enumerate`

List the memory scopes in a tenant.

Proxies `GET /v1.0/api/tenants/{tenantId}/scopes`.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tenantId` | string | No | your credential's tenant | Tenant identifier |

#### Example Request

```json
{
  "tenantId": "ten_a1b2c3"
}
```

#### Response

```json
{
  "tool": "scope_enumerate",
  "success": true,
  "statusCode": 200,
  "data": [
    {
      "scopeId": "scp_repo",
      "name": "agent-memory-repo",
      "storeProvider": "RecallDb"
    },
    {
      "scopeId": "scp_global",
      "name": "global",
      "storeProvider": "RecallDb"
    }
  ]
}
```

#### Guidance

- A scope is a named memory space. Select one by `scopeId` before any category or memory call.
- `storeProvider` tells you whether semantic search is available (`RecallDb`) or whether the
  scope is keyword-only (`Filesystem`).

### `scope_create`

Create a memory scope for a project when none exists. Use this once, at the start of a project,
after `scope_enumerate` shows no suitable scope.

Proxies `POST /v1.0/api/tenants/{tenantId}/scopes`.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tenantId` | string | No | your credential's tenant | Tenant identifier |
| `name` | string | Yes | n/a | Scope name (for example the project name) |
| `description` | string | No | null | What the scope holds |
| `storeProvider` | string | No | server default | Backing store: `RecallDb` or `Filesystem` (`Verbex` is not available yet and is rejected) |
| `embeddingEndpointId` | string | No | null | Embedding endpoint id for semantic scopes |
| `dimensionality` | integer | No | null | Embedding vector dimension |
| `filesystemLayout` | string | No | null | Layout for a `Filesystem` scope: `SingleFile`, `Hierarchy`, or `OkfBundle` |
| `targetPath` | string | No | null | Root path for a `Filesystem` scope |
| `chunkingMode` | string | No | `OnOverflow` | When to chunk oversized memory bodies for embedding: `OnOverflow`, `Always`, or `Off` |
| `chunkStrategy` | string | No | `FixedTokenCount` | Chunk splitting strategy (e.g. `FixedTokenCount`, `SentenceBased`, `ParagraphBased`, `Recursive`) |
| `chunkMaxTokens` | integer | No | 0 | Per-chunk token budget (0 = use the embedding model's resolved budget) |
| `chunkOverlapTokens` | integer | No | 64 | Token overlap between adjacent chunks |
| `rerankEndpointId` | string | No | the tenant's first cross-encoder | An inference endpoint that reranks the scope's searches: a cross-encoder (`Tei` or `Cohere` format), or a chat model for a slower, high-precision mode |
| `rerankCandidates` | integer | No | 10 | Candidates the reranker scores before the top results are kept (1..100) |
| `rerankMinScore` | number | No | null | Drop reranked hits scoring below this (0..1), so a question with no relevant memory returns nothing |
| `inferenceEndpointId` | string | No | null | Inference endpoint that answers chat in this scope (null: the tenant's first active one) |
| `queryEndpointId` | string | No | null | Inference endpoint that rewrites follow-ups, splits, and expands queries (null: the scope's chat model) |
| `conversationRewrite` | boolean | No | server default (true) | Rewrite chat follow-up questions into standalone queries |
| `queryExpansion` | string | No | server default (`Auto`) | `Off`, `On`, or `Auto` (expand searches that are not reranked) |
| `queryDecomposition` | boolean | No | server default (false) | Split multi-part questions into sub-queries |

#### Example Request

```json
{
  "tenantId": "ten_a1b2c3",
  "name": "agent-memory-repo",
  "description": "Memory for the AgentMemory project.",
  "storeProvider": "RecallDb"
}
```

#### Response

```json
{
  "tool": "scope_create",
  "success": true,
  "statusCode": 201,
  "data": {
    "scopeId": "scp_repo",
    "name": "agent-memory-repo",
    "storeProvider": "RecallDb"
  }
}
```

#### Guidance

- Enumerate first; create a scope only when none fits. One scope per project is the norm.
- For semantic search, provide an `embeddingEndpointId` and `dimensionality` that match the
  configured embedding endpoint (list them with `endpoint_enumerate`). If you omit them, the
  default RecallDb store auto-selects the tenant's embedding endpoint and its dimensionality.

### `endpoint_enumerate`

List a tenant's configured model endpoints (embedding, inference, and rerank). Use it to choose an
`embeddingEndpointId` for a semantic (`RecallDb`) scope, or to confirm whether any embedding
endpoint exists at all.

Proxies `GET /v1.0/api/tenants/{tenantId}/endpoints` (optional `kind` filter).

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tenantId` | string | No | your credential's tenant | Tenant identifier |
| `kind` | string | No | all | Filter by endpoint kind: `Embedding` or `Inference`. Rerankers are inference endpoints: `apiFormat` `Tei` or `Cohere` marks a cross-encoder (rerank only); `Ollama`, `OpenAI`, and `VLlm` models can chat and rerank. `Rerank` is accepted and lists inference endpoints |

#### Guidance

- If no embedding endpoint is configured, create a `Filesystem` (keyword-only) scope.

### `instructions`

Get the tenant's standing instructions — tenant-wide guidance the operator wants every agent to
follow. Returned in ascending `position` order.

Proxies `GET /v1.0/api/tenants/{tenantId}/instructions`.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tenantId` | string | No | your credential's tenant | Tenant identifier |

#### Example Request

```json
{
  "tenantId": "ten_a1b2c3"
}
```

#### Response

```json
{
  "tool": "instructions",
  "success": true,
  "statusCode": 200,
  "data": [
    {
      "id": "ins_1",
      "name": "House style",
      "content": "Prefer terse commit messages that reference an issue id.",
      "position": 0,
      "active": true
    }
  ]
}
```

#### Guidance

- Read the tenant's instructions early; they are always-on guidance to honor without being asked.
- Instructions are tenant-wide and apply across every scope in the tenant.

### `guide`

Get the operating guide for a scope: its categories, their usage instructions, and store
capabilities. **Call this before writing memory.**

Proxies `GET /v1.0/api/tenants/{tenantId}/scopes/{scopeId}/guide`.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tenantId` | string | No | your credential's tenant | Tenant identifier |
| `scopeId` | string | Yes | n/a | Scope identifier |

#### Example Request

```json
{
  "tenantId": "ten_a1b2c3",
  "scopeId": "scp_repo"
}
```

#### Response

```json
{
  "tool": "guide",
  "success": true,
  "statusCode": 200,
  "data": {
    "scopeId": "scp_repo",
    "categories": [
      {
        "categoryId": "cat_layout",
        "name": "layout",
        "description": "Where things live in the repository.",
        "instructions": "Write one memory per subsystem. Use the subsystem path as the slug."
      }
    ],
    "capabilities": {
      "supportsKeyword": true,
      "supportsSemantic": true,
      "supportsHybrid": true,
      "requiresEmbedding": true,
      "description": "RecallDb store: semantic + keyword + hybrid search."
    }
  }
}
```

#### Guidance

- Treat category `instructions` as the contract for what and how to write.
- `capabilities` tells you which search modes the scope supports (`Semantic` / `Hybrid` require a RecallDb store).
- Tenant-wide standing guidance is separate — fetch it with the `instructions` tool and honor it without being asked.

### `category_enumerate`

List categories in a scope, including their usage instructions.

Proxies `GET /v1.0/api/tenants/{tenantId}/scopes/{scopeId}/categories`.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tenantId` | string | No | your credential's tenant | Tenant identifier |
| `scopeId` | string | Yes | n/a | Scope identifier |

#### Example Request

```json
{
  "tenantId": "ten_a1b2c3",
  "scopeId": "scp_repo"
}
```

#### Response

```json
{
  "tool": "category_enumerate",
  "success": true,
  "statusCode": 200,
  "data": [
    {
      "categoryId": "cat_layout",
      "name": "layout",
      "description": "Where things live in the repository.",
      "instructions": "Write one memory per subsystem."
    }
  ]
}
```

#### Guidance

- Prefer an existing category over creating a new one.
- `guide` returns the same category information plus the scope's store capabilities; use it for onboarding.

### `category_create`

Create a category in a scope.

Proxies `POST /v1.0/api/tenants/{tenantId}/scopes/{scopeId}/categories`.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tenantId` | string | No | your credential's tenant | Tenant identifier |
| `scopeId` | string | Yes | n/a | Scope identifier |
| `name` | string | Yes | n/a | Category name (unique within the scope; accepted by `memory_search` as a filter) |
| `description` | string | No | null | What the category holds |
| `instructions` | string | No | null | When and how to write memories in this category |

#### Example Request

```json
{
  "tenantId": "ten_a1b2c3",
  "scopeId": "scp_repo",
  "name": "build-commands",
  "description": "How to build, test, and run the project.",
  "instructions": "One memory per command group. Slug by tool, e.g. build, test, lint."
}
```

#### Response

```json
{
  "tool": "category_create",
  "success": true,
  "statusCode": 201,
  "data": {
    "categoryId": "cat_build",
    "name": "build-commands"
  }
}
```

#### Guidance

- Provide `instructions` so future agents know when and how to write into this category.
- Create categories sparingly; too many fragments dilutes recall.

### `memory_enumerate`

List memory summaries in a scope. Summaries are token-cheap and do not include the full body.

Proxies `GET /v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories` with optional
`category` and `maxResults` query parameters.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tenantId` | string | No | your credential's tenant | Tenant identifier |
| `scopeId` | string | Yes | n/a | Scope identifier |
| `category` | string | No | null | Optional `categoryId` filter |
| `maxResults` | integer | No | server default | Maximum summaries to return |

#### Example Request

```json
{
  "tenantId": "ten_a1b2c3",
  "scopeId": "scp_repo",
  "category": "cat_layout",
  "maxResults": 50
}
```

#### Response

```json
{
  "tool": "memory_enumerate",
  "success": true,
  "statusCode": 200,
  "data": [
    {
      "id": "mem_1",
      "slug": "filesystem-layout",
      "title": "Where things live in the repo",
      "summary": "src/ holds the server and MCP projects; docs/ holds plans.",
      "categoryId": "cat_layout"
    }
  ]
}
```

#### Guidance

- Enumerate summaries first; call `memory_read` only for the memories you actually need.
- Filter by `category` to keep responses small.

### `memory_read`

Read a single memory by id, returning the full body.

Proxies `GET /v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories/{memoryId}`.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tenantId` | string | No | your credential's tenant | Tenant identifier |
| `scopeId` | string | Yes | n/a | Scope identifier |
| `memoryId` | string | Yes | n/a | Memory identifier |

#### Example Request

```json
{
  "tenantId": "ten_a1b2c3",
  "scopeId": "scp_repo",
  "memoryId": "mem_1"
}
```

#### Response

```json
{
  "tool": "memory_read",
  "success": true,
  "statusCode": 200,
  "data": {
    "id": "mem_1",
    "slug": "filesystem-layout",
    "title": "Where things live in the repo",
    "summary": "src/ holds the server and MCP projects; docs/ holds plans.",
    "body": "src/ holds NotDory.Core, NotDory.Server, and NotDory.McpServer. docs/ holds the plan.",
    "categoryId": "cat_layout",
    "tags": ["layout"],
    "links": ["build-commands"]
  }
}
```

#### Guidance

- This is the only tool that returns full memory bodies; use it deliberately.

### `memory_upsert`

Create or update a memory. **Idempotent on `(scope, category, slug)`** — re-writing the
same slug updates the memory in place rather than duplicating it.

Proxies `POST /v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories`.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tenantId` | string | No | your credential's tenant | Tenant identifier |
| `scopeId` | string | Yes | n/a | Scope identifier |
| `category` | string | Yes | n/a | Target category: a name (created in the scope if new, matched ignoring case) or a `cat_` id. `categoryId` is accepted as an alias |
| `slug` | string | Yes | n/a | Stable, link-addressable slug; re-writing updates in place |
| `body` | string | Yes | n/a | The memory content |
| `title` | string | No | null | Human-readable title |
| `summary` | string | No | null | One-line recall hook returned in list/search |
| `type` | string | No | null | One of `User`, `Feedback`, `Project`, `Reference` |
| `links` | string[] | No | [] | Slugs of related memories; chat follows these (and `[[slug]]` references in the body) for context |
| `supersedes` | string[] | No | [] | Slugs of memories in the scope that this memory replaces; search ranks each replaced memory after this one and marks it `supersededBy` |

#### Example Request

```json
{
  "tenantId": "ten_a1b2c3",
  "scopeId": "scp_repo",
  "categoryId": "cat_layout",
  "slug": "filesystem-layout",
  "title": "Where things live in the repo",
  "summary": "src/ holds the server and MCP projects; docs/ holds plans.",
  "body": "src/ holds NotDory.Core, NotDory.Server, and NotDory.McpServer. docs/ holds the plan.",
  "type": "Project"
}
```

#### Response

```json
{
  "tool": "memory_upsert",
  "success": true,
  "statusCode": 200,
  "data": {
    "id": "mem_1",
    "slug": "filesystem-layout",
    "version": 2,
    "supersedes": [],
    "supersededBy": null,
    "similarMemories": [
      { "id": "mem_4", "slug": "repo-layout", "title": "Repo layout", "categoryId": "cat_layout", "similarity": 0.94 }
    ]
  }
}
```

`similarMemories` appears only on scopes with semantic search and only when an existing memory is very close to
the one written (server setting `retrieval.duplicateSimilarityThreshold`, default 0.85).

#### Guidance

- Choose a stable, descriptive slug so repeated writes update one memory.
- If `similarMemories` lists a memory that says the same thing, update that memory (reuse its slug) and delete the
  new one, or keep the new one and pass the old slug in `supersedes`.
- When a fact changes and the old memory should stay for history, write the new memory with `supersedes`.
- Provide a crisp `summary`; it is the recall hook shown in enumerate and search results.
- Do not store secrets, credentials, tokens, or raw sensitive data as memory content.

### `memory_search`

Search a scope's memory. Returns ranked results.

Proxies `POST /v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories/search`.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tenantId` | string | No | your credential's tenant | Tenant identifier |
| `scopeId` | string | Yes | n/a | Scope identifier |
| `queryText` | string | Yes | n/a | The search query |
| `mode` | string | No | server default | `Keyword`, `Semantic`, or `Hybrid` |
| `topK` | integer | No | server default | Maximum results to return |
| `categoryName` | string | No | null | Optional category filter: name or `cat_` id (sent as `categoryFilter`). An unknown category returns 400. |
| `minScore` | number | No | null | Drop hits scoring below this. Hybrid scores are fused and normalized to 0..1 |
| `recencyWeight` | number | No | 0.1 | Hybrid only: weight (0..1) of a signal favoring recently written memories; 0 disables |
| `superseded` | string | No | `Demote` | `Demote` ranks a replaced memory right after its replacement; `Hide` drops it; `Include` keeps the order and only marks it |
| `linkExpansion` | integer | No | 0 | Add up to this many linked memories (0..10) after the results that link to them (marked `linkedFrom`) |
| `diversity` | number | No | 0 | 0..1: higher values move results that repeat a higher-ranked one below other relevant memories |
| `rerank` | boolean | No | scope default | Rerank with the scope's rerank endpoint (default: when the scope has one) |
| `minRerankScore` | number | No | scope `rerankMinScore` | Drop reranked hits scoring below this (0..1) |
| `additionalQueries` | string[] | No | [] | Up to 4 extra queries searched alongside `queryText` and fused; pass the parts of a multi-part question |
| `additionalQueryWeight` | number | No | server setting (1.0) | Fusion weight of each additional query relative to `queryText`'s 1.0, 0 to 1 |
| `subQueries` | object[] | No | [] | Up to 4 extra queries `{ text, weight?, mode? }` with their own fusion weight (0 to 1, default 1) and optional mode |
| `decompose` | boolean | No | the scope's `queryDecomposition` (false) | Split a multi-part question into sub-queries first |
| `expand` | boolean | No | the scope's `queryExpansion` (Auto) | Draft a hypothetical answer (searched by vector) and keywords (searched as text), fused below `queryText`; unset follows the scope, which by default expands searches that are not reranked |
| `expansionWeight` | number | No | server setting (0.5) | Fusion weight of the `expand` forms relative to `queryText`'s 1.0, 0 to 1 |

#### Example Request

```json
{
  "tenantId": "ten_a1b2c3",
  "scopeId": "scp_repo",
  "queryText": "how do I run the tests",
  "mode": "Hybrid",
  "topK": 5,
  "categoryName": "build-commands"
}
```

#### Response

```json
{
  "tool": "memory_search",
  "success": true,
  "statusCode": 200,
  "data": {
    "hits": [
      {
        "storeKey": "mem_7",
        "slug": "run-tests",
        "title": "Build and run the test suite",
        "snippet": "Build: dotnet build src/NotDory.sln -c Release. Test: dotnet run --project src/Test.Automated…",
        "score": 0.83,
        "vectorScore": 0.61,
        "textScore": 0.09,
        "vectorRank": 1,
        "textRank": 2,
        "memoryId": "mem_7",
        "rerankScore": null,
        "supersededBy": null,
        "linkedFrom": null
      }
    ],
    "effectiveMode": "Hybrid",
    "reranked": false
  }
}
```

#### Guidance

- `Semantic` and `Hybrid` modes require a RecallDB-backed scope; `Keyword` works on any store.
- A hit with `supersededBy` is outdated: prefer the memory it names.
- On a reranked search (`reranked: true`), `score` is the reranker's relevance score; an empty result under a
  `minRerankScore` means no memory answers the question.
- Search before writing to avoid creating a duplicate memory under a new slug.

### `memory_delete`

Delete a memory by id.

Proxies `DELETE /v1.0/api/tenants/{tenantId}/scopes/{scopeId}/memories/{memoryId}`.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `tenantId` | string | No | your credential's tenant | Tenant identifier |
| `scopeId` | string | Yes | n/a | Scope identifier |
| `memoryId` | string | Yes | n/a | Memory identifier |

#### Example Request

```json
{
  "tenantId": "ten_a1b2c3",
  "scopeId": "scp_repo",
  "memoryId": "mem_1"
}
```

#### Response

```json
{
  "tool": "memory_delete",
  "success": true,
  "statusCode": 204,
  "data": null
}
```

#### Guidance

- Delete memories that are proven wrong or obsolete rather than leaving stale guidance.

## Error Behavior

A request with no access key is rejected at the transport layer before any tool runs (present
the access key as `Authorization: Bearer <accessKey>` or `x-access-key`):

```text
HTTP 401
```

A missing required argument raises an error from the tool:

```text
Argument 'tenantId' is required.
```

Downstream REST failures are returned in the envelope with `success: false` and the
upstream `statusCode`:

```json
{
  "tool": "memory_read",
  "success": false,
  "statusCode": 404,
  "data": { "error": "Memory 'mem_missing' not found." }
}
```

```json
{
  "tool": "scope_enumerate",
  "success": false,
  "statusCode": 403,
  "data": { "error": "Credential is not authorized for tenant 'ten_other'." }
}
```

## Related Documents

- [CONNECTING_AGENTS.md](CONNECTING_AGENTS.md) — connect Claude Code, Cursor, and generic
  MCP clients to NotDory, including the `notdory mcp install` helper.
- [REST_API.md](REST_API.md): the REST API each tool proxies, including request limits and error codes.
- [../SEARCH_PIPELINE.md](../SEARCH_PIPELINE.md): how search and chat retrieval work, stage by stage.
- [NOTDORY_PLAN.md](../archive/NOTDORY_PLAN.md): the original product plan (historical).
</content>
</invoke>
