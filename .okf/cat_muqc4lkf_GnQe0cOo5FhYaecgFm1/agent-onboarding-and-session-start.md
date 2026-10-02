---
type: "Project"
title: "How agents are onboarded: server instructions, session_start, SessionStart hook"
description: "MCP initialize carries instructions; session_start resolves/creates the scope by remote repo name then folder; text is admin-editable via agent-protocol."
timestamp: "2026-10-02T02:20:11.8961820Z"
created: "2026-10-02T02:20:11.4926610Z"
slug: "agent-onboarding-and-session-start"
category: "cat_muqc4lkf_GnQe0cOo5FhYaecgFm1"
links: ["system-overview", "stale-isis-tenant-instruction"]
version: "1"
salience: "0.5"
---
- The MCP initialize result carries server instructions (call session_start, search before acting, save as you go, audit on save).
- session_start (MCP tool; POST/GET /v1.0/api/session, SessionStartService) matches a scope by project name ignoring case/punctuation, then the git remote's repository name, then the directory name (directory never creates a scope); creates one if missing. Returns protocol, categories, effective instructions, recent memories; format=text renders markdown. tenantId is optional on every MCP tool.
- Claude Code: scripts/*/install-claude registers the server at user scope and installs a SessionStart hook (notdory-claude-hook.sh/.ps1) that injects session context before the first turn; `notdory mcp install` does the same (--no-session-hook, --rest-url).
- Everything sent to the model on connect (instructions + every tool description) is editable by a system admin in the dashboard (Agent onboarding) or GET/PUT /v1.0/api/agent-protocol; defaults in Core/Helpers/AgentToolCatalog.cs and AgentProtocol.cs. The MCP server re-reads every 30 s, re-registers tools, and sends tools/list_changed.
- Instructions: tenant-global plus scope instructions merged by InstructionResolver (Append|Replace|Hide).
