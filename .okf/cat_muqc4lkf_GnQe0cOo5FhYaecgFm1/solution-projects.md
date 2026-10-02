---
type: "Reference"
title: "Projects in src/NotDory.sln and the dashboard"
description: "Map of src/ projects (Core, Server, McpServer, Test.*) and where key code lives."
timestamp: "2026-10-02T03:41:16.3759140Z"
created: "2026-10-02T02:19:52.9845810Z"
slug: "solution-projects"
category: "cat_muqc4lkf_GnQe0cOo5FhYaecgFm1"
links: ["system-overview", "dashboard-navigation"]
version: "2"
salience: "0.5"
---
All .NET projects target net10.0, version 0.1.0.
- src/NotDory.Core:
  - Models/ for the models.
  - Enums/ for the enums; every enum is named *Enum.
  - Database/ for the database drivers (Sqlite, Mysql, Postgresql, SqlServer) and Migrations/.
  - Stores/ for the memory stores: RecallDb/, Filesystem/, and MirroredMemoryStore.cs.
  - Recall/ for the model clients: EmbeddingService, InferenceService, RerankService, ModelClientFactory (via PolyPrompt 2.7.1), and MemoryChunker (via TextChunker).
  - Health/HealthCheckService.
  - Helpers/: AgentProtocol; AgentToolCatalog, which holds the default MCP tool descriptions; DefaultInstructionText; and IdGenerator, which uses PrettyId.
  - Observability.
- src/NotDory.Server is the Watson REST API:
  - Routes/*Routes.cs, one file per resource.
  - Services/: MemoryService, MemoryChatService, SessionStartService, ScopeProvisioner, QueryDecomposer, QueryExpander, QueryPreparer, ConversationRewriter, SearchRefiner, DefaultSeeder, RetentionService, and the Auth* services.
  - Settings/: NotDorySettings and its sections.
  - Models/: request and response DTOs.
- src/NotDory.McpServer is the Voltaic MCP server. NotDoryMcpServer.cs registers the tools (McpToolRegistration), and McpInstaller.cs implements `notdory mcp install`.
- src/Test.Shared holds the Touchstone test suites (NotDorySuites.cs plus *Suite.cs) and RecordingMemoryStore, a stub primary store.
- src/Test.Automated is the console test runner.
- src/Test.Benchmark is a black-box benchmark harness that uses REST/MCP only and references no NotDory assemblies.
- dashboard/ is React 19 + Vite 6 + react-router 7 + i18next, written in plain JSX (no TypeScript). The nav is defined in config/navConfig.jsx (see [[dashboard-navigation]]). npm may not be on PATH in agent shells; add the nvm node bin directory.
- docker/ has the compose stack, per-service Dockerfiles, factory reset, and the demo seed.
- docs/ has REST_API.md, MCP_API.md, CONNECTING_AGENTS.md, and the per-harness INSTRUCTIONS_FOR_*.md.
- scripts/ has per-OS install and remove scripts for each harness.
- archive/ holds old plans.
