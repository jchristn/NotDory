---
type: "Project"
title: "MCP server is a thin authenticated proxy over the REST API"
description: "All logic lives in NotDory.Server; NotDory.McpServer only authenticates, maps tools to REST calls, and publishes editable tool text."
timestamp: "2026-10-02T02:21:29.1517710Z"
created: "2026-10-02T02:21:28.8708300Z"
slug: "mcp-server-proxies-rest"
category: "cat_muqc4pnc_EkpVw6agqqeOD8SfT90"
links: ["system-overview", "agent-onboarding-and-session-start"]
version: "1"
salience: "0.5"
---
NotDory.McpServer is a standalone process that authenticates the caller's access key (McpCallerCredentials) and proxies each tool to the NotDory REST API; it holds no business logic or database. Consequence: a new capability is implemented in NotDory.Server (route + service) first, then exposed as an MCP tool in NotDoryMcpServer.cs with its default description in Core/Helpers/AgentToolCatalog.cs. MCP tool names have no notdory_ prefix (clients namespace them). Covered by test mcp-proxy-end-to-end.
