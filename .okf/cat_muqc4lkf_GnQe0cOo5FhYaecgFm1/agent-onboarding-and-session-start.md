---
type: "Project"
title: "How agents are onboarded: server instructions, session_start, SessionStart hook"
description: "MCP initialize carries instructions (one scope per project, onboard new or thin scopes); session_start resolves or creates the scope, takes the repo-root path for the OKF mirror, and adds a notice for new or empty scopes; the text is admin-editable."
timestamp: "2026-10-02T15:52:40.7570352Z"
created: "2026-10-02T02:20:11.4926610Z"
slug: "agent-onboarding-and-session-start"
category: "cat_muqc4lkf_GnQe0cOo5FhYaecgFm1"
links: ["system-overview", "stale-isis-tenant-instruction", "filesystem-mirror"]
version: "2"
salience: "0.5"
---
- The MCP initialize result carries the server instructions:
  - Call session_start, search before acting, save as you go, and audit on save.
  - Since 2026-10-02: keep one scope per project. Onboard a new, empty, or thin scope: examine the project, describe the scope (scope_update), create categories with instructions (category_create), and save memories.
- session_start (MCP tool; POST/GET /v1.0/api/session; SessionStartService) matches a scope by project name, ignoring case and punctuation. It then tries the git remote's repository name, then the directory name; the directory name never creates a scope. If nothing matches, it creates a scope.
  - It returns the protocol, categories, effective instructions, and recent memories; format=text renders markdown. tenantId is optional on every MCP tool.
  - It takes `path`, the repository root's absolute path. A scope it creates mirrors to <path>/.okf when the server can see the directory (see [[filesystem-mirror]]).
  - It adds a `notice` when the scope is new or has no memories, and when a mirror couldn't be set up.
- Claude Code: scripts/*/install-claude registers the server at user scope and installs a SessionStart hook (notdory-claude-hook.sh/.ps1). The hook injects session context before the first turn and sends `path`. `notdory mcp install` does the same (--no-session-hook, --rest-url).
- Everything sent to the model on connect (the instructions and every tool description) is editable by a system admin. Edit it in the dashboard (Agent onboarding) or through GET/PUT /v1.0/api/agent-protocol. The defaults are in Core/Helpers/AgentToolCatalog.cs and AgentProtocol.cs. The MCP server re-reads them every 30 s, re-registers its tools, and sends tools/list_changed.
- Instructions: tenant-global and scope instructions are merged by InstructionResolver (Append|Replace|Hide).
