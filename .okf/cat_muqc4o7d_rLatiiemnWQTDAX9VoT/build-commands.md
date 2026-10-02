---
type: "Reference"
title: "Building the backend and dashboard locally"
description: "./build.sh = dotnet build src/NotDory.sln -c Release + dashboard npm ci && npm run build; .bat twins for Windows."
timestamp: "2026-10-02T02:20:22.8609950Z"
created: "2026-10-02T02:20:22.5322140Z"
slug: "build-commands"
category: "cat_muqc4o7d_rLatiiemnWQTDAX9VoT"
links: ["test-commands", "docker-stack", "publish-images"]
version: "1"
salience: "0.5"
---
Local build: `./build.sh` (or build.bat) runs `dotnet build src/NotDory.sln -c Release` and then `cd dashboard && npm ci && npm run build`. Requires the .NET 10 SDK and Node. Dashboard dev server: `cd dashboard && npm run dev`; lint: `npm run lint`.
Every script has a .sh (macOS/Linux, must be executable; see commit 59e19d6) and a .bat (Windows) twin. Keep them in sync when changing either.
Local server config: notdory.json at the repo root (REST 127.0.0.1:8700, Sqlite data/notdory.db, RecallDB http://127.0.0.1:8600). Values marked "override-via-env" are secrets supplied via environment variables; never commit real values.
