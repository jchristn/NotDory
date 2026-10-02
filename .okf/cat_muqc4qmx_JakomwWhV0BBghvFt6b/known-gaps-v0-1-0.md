---
type: "Project"
title: "Known gaps in v0.1.0 alpha"
description: "No test.sh or docker/update.sh; the mirror is one-way; the dashboard nav and chart are not browser-tested; there may be a stray \"~\" directory on the remote host; tenant instructions are stale; alpha has no migration guarantees."
timestamp: "2026-10-02T16:01:27.6528406Z"
created: "2026-10-02T02:21:43.2403700Z"
slug: "known-gaps-v0-1-0"
category: "cat_muqc4qmx_JakomwWhV0BBghvFt6b"
links: ["script-pairs", "filesystem-mirror", "dashboard-navigation", "docker-desktop-vm-disk-locked", "okf-mirror-in-repo", "stale-isis-tenant-instruction"]
version: "6"
salience: "0.5"
---
As of 2026-10-02 (commit a0ce09f):
- Script pairs are incomplete: test.bat has no test.sh, and docker/update.bat has no update.sh.
- The filesystem mirror is one-way. Edits made directly to the .okf files are not imported into RecallDB, and the next upsert of that memory overwrites them.
- The dashboard nav consolidation and Home's per-scope chart labels were checked with lint and build only. Neither has been clicked through in a browser.
- The old targetPath "~/Code/NotDory/" may have left a stray OKF bundle under a literal "~" directory in the working directory of the remote server (view.homedns.org). If it is still there, delete it by hand. The NotDory scope still stores that targetPath, but its mirror is off, so nothing is written there.
- The default tenant's global instructions (Start here, Tools, Memory model, Recall) still mention Isis and Verbex. The Tools instruction also omits session_start's path parameter. See [[stale-isis-tenant-instruction]].
- v0.1.0 is ALPHA: APIs, schemas, MCP tool names, and storage layouts may change between 0.1.x builds without migration paths.
Resolved:
- Verbex was removed.
- The full suite passed on 2026-10-02 with 512 tests: 509 passed and 3 skipped, the skipped ones being live DB tests with local Docker down.
- The v0.1.0 server, mcp, and dashboard images built from ea39a41 were pushed and deployed to view.homedns.org on 2026-10-02. Its session_start protocol now carries the path and one-scope-per-project guidance.
