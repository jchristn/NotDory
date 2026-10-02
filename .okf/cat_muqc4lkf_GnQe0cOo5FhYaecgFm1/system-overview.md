---
type: "Project"
title: "NotDory system overview and process topology"
description: "NotDory = agent memory platform: MCP server proxies a Watson REST server backed by Postgres metadata + RecallDB content."
timestamp: "2026-10-02T02:19:48.4712770Z"
created: "2026-10-02T02:19:44.9416210Z"
slug: "system-overview"
category: "cat_muqc4lkf_GnQe0cOo5FhYaecgFm1"
links: ["solution-projects", "memory-store-providers", "retrieval-pipeline"]
version: "1"
salience: "0.5"
---
NotDory (renamed from Isis, commit 98f27fa) is an agent memory platform, v0.1.0 ALPHA. Agents talk MCP; operators and the dashboard talk REST.

Flow: Agent harness -MCP-> nginx -> NotDory.McpServer (Voltaic 2.1.13), which authenticates the caller and proxies REST -> NotDory.Server (Watson 7.2). Operators/dashboard -REST-> nginx -> NotDory.Server.

NotDory.Server keeps its own metadata (categories+instructions, policies, seed packs, the memory link graph, slugs/titles/summaries, model endpoints, tenancy/auth, request history) in a `notdory` database. Memory content and vectors live in RecallDB (`recalldb` database, accessed via RecallDb.Sdk over HTTP) on the same shared Postgres instance. NotDory computes embeddings itself via an embedding endpoint and passes vectors to RecallDB. Inference endpoints handle chat answers, reranking, and query rewrite/split/expansion. All endpoints are health-checked.

Data model: tenant -> scopes (one project/book/domain, bound to one store) -> categories (with usage instructions) -> memories (slug, title, summary, body, type User|Feedback|Project|Reference, links, supersedes).
