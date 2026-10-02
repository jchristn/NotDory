---
type: "Reference"
title: "Docker compose stack, services, and ports"
description: "docker/compose.yaml runs Postgres, RecallDB, Ollama, rerank, NotDory server/mcp/dashboard, nginx, and observability; REST 8700, MCP 8720."
timestamp: "2026-10-02T02:20:34.0959990Z"
created: "2026-10-02T02:20:33.7326490Z"
slug: "docker-stack"
category: "cat_muqc4o7d_rLatiiemnWQTDAX9VoT"
links: ["build-commands", "publish-images"]
version: "1"
salience: "0.5"
---
docker/compose.yaml (all ports bound to 127.0.0.1): postgres 5432; recalldb-server 8600 REST, 8620 RecallDB's own MCP (not exposed to agents), 9464 metrics; recalldb-dashboard 8601; ollama + ollama-init (pulls gemma3:4b chat and all-minilm embeddings); rerank / rerank-gpu (cross-encoder); notdory-server 8700; notdory-mcp 8720 (agents connect to http://127.0.0.1:8720/mcp); notdory-dashboard; nginx-rest / nginx-mcp; observability: prometheus 9090, tempo 3200/4317/4318, loki 3100, alloy 12345, grafana 3000.
Images are pinned to jchristn77/notdory-{server,mcp,dashboard}:v0.1.0. docker/update.bat pulls the images and recreates the stack (keeps volumes). docker/factory/reset.sh|.bat DESTROYS all volumes and logs for a factory reset (--no-ollama keeps the model volume); docker/factory/seed has the demo seed pack. compose.factory.yaml is the seeded variant.
