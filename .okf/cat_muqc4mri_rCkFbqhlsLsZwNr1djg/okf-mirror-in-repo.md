---
type: "Project"
title: "The NotDory scope's OKF bundle is committed at .okf/ and agents maintain it by hand"
description: "The remote NotDory server can't see the checkout, so the scope's mirror is off. After each memory_upsert or memory_delete, the agent updates .okf/<categoryId>/<slug>.md and regenerates .okf/index.md (OKF v0.2)."
timestamp: "2026-10-02T15:46:12.5347490Z"
created: "2026-10-02T15:23:22.8433270Z"
slug: "okf-mirror-in-repo"
category: "cat_muqc4mri_rCkFbqhlsLsZwNr1djg"
links: ["filesystem-mirror", "known-gaps-v0-1-0"]
version: "2"
salience: "0.5"
---
Since 2026-10-02 this scope's Open Knowledge Format bundle is committed at <repo>/.okf/. Before that it was written to the repo root, which made the whole repository the bundle and broke OKF conformance. The NotDory server the maintainer uses is remote (view.homedns.org:8720) and can't see the local checkout. The scope's filesystemMirror is therefore off. Its old targetPath "~/Code/NotDory/" made the server write a stray bundle under a literal "~" directory in its working directory, because .NET doesn't expand "~". Newer servers reject such paths. Agents keep .okf/ in sync by hand.

Location: OKF v0.2 prescribes no location. It recommends git, and allows a bundle to be a subdirectory of a larger repository. .okf/ at the repo root is what community OKF tooling defaults to, and NotDory's mirror writes to <targetPath>/.okf.

Layout (same as NotDory's OkfBundle output, src/NotDory.Core/Stores/Filesystem/OkfDocument.cs and FilesystemMemoryStore.RegenerateOkfIndexAsync):
- .okf/<categoryId>/<slug>.md, with frontmatter in this order: type, title, description (= summary), timestamp (= lastUpdateUtc), status: "deprecated" only when superseded, created, slug, category (= categoryId), links, supersededBy (the replacing memory's id) only when superseded, version, salience.
- Every scalar is double-quoted. Timestamps use .NET "O" format with 7 fractional digits and a Z. The body follows verbatim.
- .okf/index.md has only `okf_version: "0.2"` as frontmatter. It has one "# <categoryId>" section per category, sorted case-insensitively. Each section lists "* [title](<categoryId>/<slug>.md) - <description>" sorted by path, with " (deprecated)" appended for superseded memories, then a blank line.

How to apply: after memory_upsert, write that memory's file and regenerate index.md. After memory_delete, remove the file and regenerate index.md. When a memory supersedes another, rewrite the old file with status "deprecated" and supersededBy. Commit .okf/ changes with the code they describe.
