<!-- markdownlint-disable MD033 MD041 -->
<img src="https://raw.githubusercontent.com/jchristn/NotDory/main/assets/notdory.png" width="128" alt="NotDory" />

# NotDory — Agent Memory Platform

**Version 0.1.0 — ALPHA.**

> ⚠️ **This is alpha software.** APIs, data models, storage layouts, configuration keys, MCP tool names, and dashboard surfaces **will change** — potentially in breaking ways and without migration paths between 0.1.x builds. Pin to an exact image tag (e.g. `v0.1.0`) and read `CHANGELOG.md` before updating.

NotDory gives AI models durable, structured, queryable **memory** that survives across sessions, harnesses, and projects. It exposes an **MCP** surface for agents and a **REST** surface (plus a React dashboard) for management.

Memory is organized into **scopes** (a project, a book, or "global"), **categories** (buckets with usage instructions the model reads), and **memories** (atomic notes with tags, links, provenance, and salience). Cross-cutting **policies** (a house writing style, GitHub commit rules) apply everywhere and are surfaced to the agent proactively.

## Images

| Image | Purpose |
|---|---|
| [`jchristn77/notdory-server`](https://hub.docker.com/r/jchristn77/notdory-server) | REST API (Watson 7.2) + OpenAPI. Listens on `8700`. |
| [`jchristn77/notdory-mcp`](https://hub.docker.com/r/jchristn77/notdory-mcp) | MCP server (Voltaic 2.1.13), agent-facing tools. Streamable HTTP on `8720`. |
| [`jchristn77/notdory-dashboard`](https://hub.docker.com/r/jchristn77/notdory-dashboard) | React 19 / Vite 6 management dashboard (nginx). |

All three are published for `linux/amd64` and `linux/arm64`, tagged `v0.1.0` and `latest`. Pin to `v0.1.0`.

The stack also uses [`jchristn77/recalldb-server`](https://hub.docker.com/r/jchristn77/recalldb-server) and [`jchristn77/recalldb-dashboard`](https://hub.docker.com/r/jchristn77/recalldb-dashboard) (`v0.2.1`) for memory content and hybrid search, and a shared [`ankane/pgvector`](https://hub.docker.com/r/ankane/pgvector) Postgres.

## Why

The dominant cost in agentic work is **re-acquiring context** — re-scanning a filesystem, re-reading files, re-learning conventions every session. NotDory turns that recurring cost into a one-time write plus cheap recall. Break-even is at roughly the second reuse; after that the savings compound.

## Use cases

- **Code:** remember what lives where, what a function does, and what was already done — so an agent skips the cold re-scan every session.
- **Writing / email / calendar:** NotDory is domain-agnostic; the same scope/category/memory model holds notes about a book, an inbox, or a schedule.
- **Cross-cutting guidance:** write a house style, commit rules, or review checklists once as **policies** and recall them across every project.
- **Chat with memory:** ask a scope's memory questions in natural language and get a synthesized answer with citations (RAG over stored memories), from the dashboard, the REST API, or the `chat` MCP tool, including follow-up questions.

## Backing stores

NotDory stores memory through a pluggable `IMemoryStore`, chosen **per scope**:

- **RecallDB** (default) — Postgres-backed; the only provider offering **semantic** and **hybrid** (vector + lexical) search. NotDory computes the embedding vector via a configured, health-checked embedding endpoint and passes it to RecallDB (bring-your-own-vector). Embedding dimension is fixed per scope.
- **Filesystem** — flat files (single file, a reviewable markdown hierarchy, or an Open Knowledge Format bundle) that travel inside the target repository; keyword/metadata search only.
- **RecallDB + filesystem mirror** — a RecallDB scope with `filesystemMirror` on also writes every memory, concurrently, to an Open Knowledge Format bundle at its `targetPath`. Search uses RecallDB; the bundle is a git-trackable copy. Bind-mount the target directory into `notdory-server`.

On a RecallDB scope, a search runs hybrid vector + full-text search, fuses the two rankings, and can rerank the candidates with a cross-encoder when the scope has a Rerank endpoint (start the stack with `--profile rerank` or `--profile rerank-gpu`).

## Architecture

```
Agent harness ──MCP──▶ nginx ─▶ NotDory.McpServer (Voltaic 2.1.13) ─proxy─┐
Operator/UI  ──REST──▶ nginx ─▶ NotDory.Server (Watson 7.2) ◀────────────┘
                                     │                 │
                     NotDory metadata│                 │memory content + vectors
                       (Postgres     │                 │(RecallDb.Sdk over HTTP)
                        db: notdory)  ▼                 ▼
                                ┌───────────┐    ┌──────────────┐
                                │ Postgres  │    │  RecallDB    │
                                │ (shared)  │    │  db: recalldb│
                                └───────────┘    └──────────────┘
   Embedding endpoint ◀─ NotDory computes vectors
   Inference endpoint ◀─ NotDory summarizes / compacts   (health-checked, dedup by method+URL+auth)
   (rerankers are inference endpoints: a cross-encoder or a chat model reorders search candidates)
```

- **RecallDB** is the system of record for memory content, embeddings, and retrieval, on a shared Postgres instance.
- **NotDory** owns a separate `notdory` database on that same Postgres instance for concepts RecallDB has no schema for: category instructions, policies, seed packs, the memory link graph, slugs/titles/summaries, model-endpoint configs, and request history.
- **NotDory.McpServer** authenticates the caller and proxies the NotDory REST API over loopback.

## Getting started

```bash
git clone https://github.com/jchristn/notdory
cd notdory/docker
cp .env.example .env        # then edit the secrets
docker compose up -d --build
```

One command brings up Postgres (pgvector), RecallDB + dashboard, the NotDory REST/MCP servers and dashboard, two nginx fronts, and the full observability stack (Prometheus, Tempo, Loki, Alloy, Grafana) wired together and healthy. All host-published ports bind `127.0.0.1` (loopback only).

For a seeded demo (a demo tenant, scope, categories, and policies):

```bash
docker compose -f compose.yaml -f factory/compose.factory.yaml up -d --build
```

### Host-published ports (browser-reachable)

| Service | URL | Default credentials |
|---|---|---|
| NotDory dashboard | http://localhost:8701 | login `admin@notdory.local` / `notdoryadmin` (tenant `ten_default`) |
| NotDory REST (direct) | http://localhost:8700/v1.0/api/health | `x-access-key: notdorydefaultkey` (the secret key is optional) |
| NotDory REST (via nginx) | http://localhost:8080 | — |
| NotDory MCP (via nginx) | http://localhost:8090/mcp | — |
| NotDory MCP (direct) | http://localhost:8720/mcp | — |
| RecallDB console | http://localhost:8601 | `recalldbadmin` |
| Grafana | http://localhost:3000 | `admin` / `admin` |
| Prometheus | http://localhost:9090 | — |
| Tempo API | http://localhost:3200 | — |

> These are **local development defaults**. Change every credential (Grafana password, the seeded admin password, the default credential access/secret keys, RecallDB admin key) before any shared or hosted deployment, and do not expose Prometheus/Tempo/`/metrics` on a public interface.

## Configuration

NotDory reads `notdory.json` and honors environment overrides. Key variables (see `docker/.env.example`):

| Variable | Meaning |
|---|---|
| `NOTDORY_AUTH_SEED_ADMIN_EMAIL` / `NOTDORY_AUTH_SEED_ADMIN_PASSWORD` | Email and password of the seeded bootstrap admin user (defaults `admin@notdory.local` / `notdoryadmin`). Log in for a session token via `POST /v1.0/api/token`. |
| `NOTDORY_AUTH_DEFAULT_ACCESS_KEY` / `NOTDORY_AUTH_DEFAULT_SECRET_KEY` | Access key and secret key seeded on the default tenant credential (defaults `notdorydefaultkey` / `notdorydefaultsecret`). The access key (`x-access-key` or `Authorization: Bearer`) authenticates on its own; an `x-secret-key` is validated only when sent. |
| `NOTDORY_DB_TYPE` / `NOTDORY_DB_SERVER` / `NOTDORY_DB_DATABASE` / `NOTDORY_DB_USERNAME` / `NOTDORY_DB_PASSWORD` | NotDory metadata database (Postgresql in Docker). |
| `NOTDORY_REST_PORT` | REST listener port (default `8700`). |
| `NOTDORY_MCP_PORT` / `NOTDORY_MCP_REST_HOSTNAME` / `NOTDORY_MCP_REST_PORT` | MCP transport port and the REST server it proxies. |
| `RECALLDB_ADMIN_KEY` | RecallDB admin key NotDory presents server-side. |
| `GF_SECURITY_ADMIN_PASSWORD` | Grafana admin password. |

## Observability

Watson 7.2 emits the full HTTP surface as metrics and traces; NotDory extends it with application meters (memory read/write/search, tokens-served estimate, recall hit-rate, embedding/inference latency, endpoint health gauges). Prometheus scrapes NotDory and RecallDB, Tempo ingests traces, Loki aggregates container logs via Alloy, and Grafana is provisioned as code with a **NotDory** dashboard folder (starting with an Overview dashboard). Grafana starts only after Prometheus and Tempo are healthy.

## License

MIT — see `LICENSE.md`.
