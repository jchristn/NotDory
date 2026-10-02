---
type: "Project"
title: "Shell scripts come in .sh/.bat pairs and per-OS folders"
description: "Every repo script has a Windows .bat and an executable .sh twin; harness installers live in scripts/{windows,macos,linux}."
timestamp: "2026-10-02T02:21:16.6958000Z"
created: "2026-10-02T02:21:16.4000830Z"
slug: "script-pairs"
category: "cat_muqc4mri_rCkFbqhlsLsZwNr1djg"
links: ["build-commands"]
version: "1"
salience: "0.5"
---
Root build scripts (build, build-all, build-server, build-mcp, build-dashboard), benchmark scripts, and factory reset scripts each exist as .bat and .sh; .sh files must be committed executable (chmod +x, commit 59e19d6). Harness installers live in scripts/windows (*.bat), scripts/macos and scripts/linux (*.sh; macOS and Linux copies are identical), one install-<agent> and remove-<agent> per agent (claude, codex, cursor, gemini, mux). Installers are idempotent, back up the config, preserve other entries, and honor NOTDORY_MCP_URL, NOTDORY_ACCESS_KEY, and per-harness config overrides. Change all OS variants together.
