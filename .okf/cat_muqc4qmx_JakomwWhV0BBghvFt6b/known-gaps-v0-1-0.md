---
type: "Project"
title: "Known gaps in v0.1.0 alpha"
description: "No test.sh or docker/update.sh counterparts; mirror is write-only; new dashboard nav not browser-tested; remote deployment needs the new images; alpha has no migration guarantees."
timestamp: "2026-10-02T04:11:27.8356180Z"
created: "2026-10-02T02:21:43.2403700Z"
slug: "known-gaps-v0-1-0"
category: "cat_muqc4qmx_JakomwWhV0BBghvFt6b"
links: ["script-pairs", "filesystem-mirror", "dashboard-navigation", "docker-desktop-vm-disk-locked"]
version: "3"
salience: "0.5"
---
As of 2026-10-01 (commit f38cbaf):
- Script pairs are incomplete: test.bat has no test.sh, and docker/update.bat has no update.sh.
- The filesystem mirror is one-way. Edits made directly to the OKF files are not imported into RecallDB, and the next upsert of that memory overwrites them.
- The dashboard nav consolidation was checked with lint and build only. It has not been clicked through in a browser.
- The v0.1.0 images (server, mcp, dashboard) were pushed on 2026-10-01. The maintainer's remote deployment (view.homedns.org) still has to pull them and recreate its containers before agents see filesystemMirror and the new dashboard.
- v0.1.0 is ALPHA: APIs, schemas, MCP tool names, and storage layouts may change between 0.1.x builds without migration paths.
Resolved: Verbex was removed. The full suite (510 tests, including live PostgreSQL, MySQL, and SQL Server) passed on 2026-10-01 after Docker was fixed, so Migration012 is verified on all four databases.
