---
type: "Reference"
title: "Publishing multi-arch Docker images"
description: "build-server.sh/build-mcp.sh/build-dashboard.sh <tag> push jchristn77/notdory-* for amd64+arm64 via a cloud buildx builder."
timestamp: "2026-10-02T02:20:38.6847820Z"
created: "2026-10-02T02:20:38.3888200Z"
slug: "publish-images"
category: "cat_muqc4o7d_rLatiiemnWQTDAX9VoT"
links: ["docker-stack"]
version: "1"
salience: "0.5"
---
`./build-server.sh <tag>`, `./build-mcp.sh <tag>`, `./build-dashboard.sh <tag>` (and `build-all.sh`) run `docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8` with tags latest and <tag>, push to Docker Hub as jchristn77/notdory-server|mcp|dashboard, then pull both tags locally. Example tag: v0.1.0. Dockerfiles: docker/server/Dockerfile, dashboard/Dockerfile, and the MCP Dockerfile under docker/mcp. Pushing is outward-facing; only do it when the maintainer asks. DOCKERHUB_README.md is the Docker Hub page text.
