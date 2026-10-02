---
type: "Project"
title: "Known gaps in v0.1.0 alpha"
description: "No test.sh or docker/update.sh; the mirror is one-way; the dashboard nav and chart are not browser-tested; the remote deployment needs new images and has a stray \"~\" directory; alpha has no migration guarantees."
timestamp: "2026-10-02T15:52:46.7550884Z"
created: "2026-10-02T02:21:43.2403700Z"
slug: "known-gaps-v0-1-0"
category: "cat_muqc4qmx_JakomwWhV0BBghvFt6b"
links: ["script-pairs", "filesystem-mirror", "dashboard-navigation", "docker-desktop-vm-disk-locked", "okf-mirror-in-repo"]
version: "4"
salience: "0.5"
---
As of 2026-10-02 (commit 9a5524d):
- Script pairs are incomplete: test.bat has no test.sh, and docker/update.bat has no update.sh.
- The filesystem mirror is one-way. Edits made directly to the .okf files are not imported into RecallDB, and the next upsert of that memory overwrites them.
- The dashboard nav consolidation and Home's per-scope chart labels were checked with lint and build only. Neither has been clicked through in a browser.
- The maintainer's remote deployment (view.homedns.org) runs images that predate 9a5524d. It needs new images built, pushed, and pulled before agents get the .okf location, the session_start `path` parameter, mirror-by-default, and the '~' rejection.
- That server also has a stray OKF bundle under a literal "~" directory in its working directory, left by the old targetPath "~/Code/NotDory/". Delete it by hand on the host.
- v0.1.0 is ALPHA: APIs, schemas, MCP tool names, and storage layouts may change between 0.1.x builds without migration paths.
Resolved: Verbex was removed. The full suite passed on 2026-10-02 with 512 tests: 509 passed and 3 skipped, the skipped ones being live DB tests with local Docker down.
