---
okf_version: "0.2"
---
# cat_muqc4lkf_GnQe0cOo5FhYaecgFm1

* [How agents are onboarded: server instructions, session_start, SessionStart hook](cat_muqc4lkf_GnQe0cOo5FhYaecgFm1/agent-onboarding-and-session-start.md) - MCP initialize carries instructions (one scope per project, onboard new or thin scopes); session_start resolves or creates the scope, takes the repo-root path for the OKF mirror, and adds a notice for new or empty scopes; the text is admin-editable.
* [Authentication and tenancy model](cat_muqc4lkf_GnQe0cOo5FhYaecgFm1/auth-model.md) - Users log in with email+password for a bearer session token; MCP/automation use a credential access key (x-access-key or Bearer).
* [Dashboard nav: 7 tabbed hubs defined in navConfig.jsx](cat_muqc4lkf_GnQe0cOo5FhYaecgFm1/dashboard-navigation.md) - Workspace (Home, Memory, Recall) + Administration (Models, Access, Monitoring, System); tabs via ?tab=; old URLs redirect via legacyPaths.
* [RecallDb scopes can mirror every memory to an OKF bundle in <targetPath>/.okf (filesystemMirror)](cat_muqc4lkf_GnQe0cOo5FhYaecgFm1/filesystem-mirror.md) - Scope.FilesystemMirror + TargetPath (the repo root): MirroredMemoryStore writes RecallDB and an OKF v0.2 bundle at <targetPath>/.okf concurrently; search uses RecallDB only; on by default when a targetPath is given.
* [Pluggable IMemoryStore providers chosen per scope](cat_muqc4lkf_GnQe0cOo5FhYaecgFm1/memory-store-providers.md) - RecallDB (semantic+hybrid, default, optional OKF filesystem mirror) or Filesystem (keyword, git-trackable). Verbex was removed 2026-10-01.
* [A scope writes to exactly one store; no dual-write or cross-scope search](cat_muqc4lkf_GnQe0cOo5FhYaecgFm1/one-store-per-scope-no-mirroring.md) - MemoryStoreFactory picks one IMemoryStore per scope; no mirroring to Filesystem + RecallDB, and memory_search takes a single scopeId. (deprecated)
* [Search and chat retrieval pipeline stages](cat_muqc4lkf_GnQe0cOo5FhYaecgFm1/retrieval-pipeline.md) - Parallel vector + full-text, weighted RRF with recency, chunk rollup, optional rerank, supersession, link expansion; documented in SEARCH_PIPELINE.md.
* [Projects in src/NotDory.sln and the dashboard](cat_muqc4lkf_GnQe0cOo5FhYaecgFm1/solution-projects.md) - Map of src/ projects (Core, Server, McpServer, Test.*) and where key code lives.
* [NotDory system overview and process topology](cat_muqc4lkf_GnQe0cOo5FhYaecgFm1/system-overview.md) - NotDory = agent memory platform: MCP server proxies a Watson REST server backed by Postgres metadata + RecallDB content.

# cat_muqc4mri_rCkFbqhlsLsZwNr1djg

* [Changelog and docs updates accompany every feature](cat_muqc4mri_rCkFbqhlsLsZwNr1djg/changelog-practice.md) - Add a bolded-lead bullet under "## [0.1.0] - ALPHA (in progress)" in CHANGELOG.md and update REST_API.md / MCP_API.md / dashboard / tests together.
* [C# code style used throughout src/](cat_muqc4mri_rCkFbqhlsLsZwNr1djg/csharp-code-style.md) - No `var`, usings inside namespace, #region blocks, XML docs on everything, ConfigureAwait(false), String.Equals with StringComparison.
* [The NotDory scope's OKF bundle is committed at .okf/ and agents maintain it by hand](cat_muqc4mri_rCkFbqhlsLsZwNr1djg/okf-mirror-in-repo.md) - The remote NotDory server can't see the checkout, so the scope's mirror is off. After each memory_upsert or memory_delete, the agent updates .okf/<categoryId>/<slug>.md and regenerates .okf/index.md (OKF v0.2).
* [Product name is NotDory (formerly Isis)](cat_muqc4mri_rCkFbqhlsLsZwNr1djg/product-naming.md) - Write "NotDory" (or lowercase "notdory" in identifiers, images, config keys); the old name Isis is retired.
* [How to add a database schema/data migration](cat_muqc4mri_rCkFbqhlsLsZwNr1djg/schema-migrations.md) - Add Database/Migrations/MigrationNNN<Name>.cs (internal sealed ISchemaMigration, Name = "yyyy-MM-dd-slug"), register it in MigrationRunner, and update all 3 setup-query files.
* [Shell scripts come in .sh/.bat pairs and per-OS folders](cat_muqc4mri_rCkFbqhlsLsZwNr1djg/script-pairs.md) - Every repo script has a Windows .bat and an executable .sh twin; harness installers live in scripts/{windows,macos,linux}.

# cat_muqc4o7d_rLatiiemnWQTDAX9VoT

* [Running the benchmark suite against an isolated stack](cat_muqc4o7d_rLatiiemnWQTDAX9VoT/benchmarks.md) - src/Test.Benchmark (retrieval, chat, agent, load, compare, prepare, stub) runs against benchmarks/docker on ports 15432/18600/18700/18720.
* [Building the backend and dashboard locally](cat_muqc4o7d_rLatiiemnWQTDAX9VoT/build-commands.md) - ./build.sh = dotnet build src/NotDory.sln -c Release + dashboard npm ci && npm run build; .bat twins for Windows.
* [Docker Desktop "storage device attachment is invalid" = Docker.raw held open by another process](cat_muqc4o7d_rLatiiemnWQTDAX9VoT/docker-desktop-vm-disk-locked.md) - If Docker Desktop won't start (VM error "storage device attachment is invalid"), lsof Docker.raw; on 2026-10-01 it was a debug Armor.Agent holding it.
* [Docker compose stack, services, and ports](cat_muqc4o7d_rLatiiemnWQTDAX9VoT/docker-stack.md) - docker/compose.yaml runs Postgres, RecallDB, Ollama, rerank, NotDory server/mcp/dashboard, nginx, and observability; REST 8700, MCP 8720.
* [Publishing multi-arch Docker images](cat_muqc4o7d_rLatiiemnWQTDAX9VoT/publish-images.md) - build-server.sh/build-mcp.sh/build-dashboard.sh <tag> push jchristn77/notdory-* for amd64+arm64 via a cloud buildx builder.
* [Running the automated test suite (Touchstone)](cat_muqc4o7d_rLatiiemnWQTDAX9VoT/test-commands.md) - dotnet run --project src/Test.Automated -c Release; tests are Touchstone cases registered in src/Test.Shared/NotDorySuites.cs.

# cat_muqc4pnc_EkpVw6agqqeOD8SfT90

* [MCP server is a thin authenticated proxy over the REST API](cat_muqc4pnc_EkpVw6agqqeOD8SfT90/mcp-server-proxies-rest.md) - All logic lives in NotDory.Server; NotDory.McpServer only authenticates, maps tools to REST calls, and publishes editable tool text.
* [Rerankers are inference endpoints, not a separate endpoint kind](cat_muqc4pnc_EkpVw6agqqeOD8SfT90/rerankers-are-inference-endpoints.md) - Endpoint kinds are Embedding and Inference only; ApiFormat decides capability (Tei/Cohere = cross-encoder rerank only; Ollama/OpenAI/VLlm/Gemini chat can rerank by prompt).
* [NotDory metadata DB is separate from RecallDB content](cat_muqc4pnc_EkpVw6agqqeOD8SfT90/split-metadata-and-content-stores.md) - NotDory owns a `notdory` DB (Sqlite/Mysql/Postgres/SqlServer) for what RecallDB has no schema for; RecallDB holds content and vectors.

# cat_muqc4qmx_JakomwWhV0BBghvFt6b

* [Known gaps in v0.1.0 alpha](cat_muqc4qmx_JakomwWhV0BBghvFt6b/known-gaps-v0-1-0.md) - No test.sh or docker/update.sh; the mirror is one-way; the dashboard nav and chart are not browser-tested; there may be a stray "~" directory on the remote host; tenant instructions are stale; alpha has no migration guarantees.
* [Running tenant's "Start here" instruction still says Isis](cat_muqc4qmx_JakomwWhV0BBghvFt6b/stale-isis-tenant-instruction.md) - ins_mtw23496_mC4eOGSdSxXcMHUcgN1 in ten_default still names the product Isis; Migration011 cannot fix it because it only replaces known seeded texts.

