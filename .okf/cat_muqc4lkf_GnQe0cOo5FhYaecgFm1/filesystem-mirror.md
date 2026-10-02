---
type: "Project"
title: "RecallDb scopes can mirror every memory to an OKF bundle in <targetPath>/.okf (filesystemMirror)"
description: "Scope.FilesystemMirror + TargetPath (the repo root): MirroredMemoryStore writes RecallDB and an OKF v0.2 bundle at <targetPath>/.okf concurrently; search uses RecallDB only; on by default when a targetPath is given."
timestamp: "2026-10-02T15:52:35.3806958Z"
created: "2026-10-02T03:40:38.3992040Z"
slug: "filesystem-mirror"
category: "cat_muqc4lkf_GnQe0cOo5FhYaecgFm1"
links: ["memory-store-providers", "schema-migrations", "okf-mirror-in-repo", "agent-onboarding-and-session-start"]
version: "2"
salience: "0.5"
---
Added 2026-10-01 and reworked 2026-10-02 (commit 9a5524d). A RecallDb scope with `filesystemMirror: true` and a `targetPath` is wrapped by MemoryStoreFactory in MirroredMemoryStore (src/NotDory.Core/Stores/MirroredMemoryStore.cs). Upserts and deletes go to RecallDB and, concurrently (Task.WhenAll), to a FilesystemMemoryStore in the OkfBundle layout. That layout is always OKF, regardless of scope.FilesystemLayout.
- Location: targetPath is the repository root. NotDory appends `.okf` itself (MirroredMemoryStore.BundleDirectoryName, BundlePath(scope)), so the bundle is <targetPath>/.okf/<categoryId>/<slug>.md plus .okf/index.md. The mirror owns .okf: it regenerates index.md from every file under it, so the bundle never indexes the repository's own markdown.
- Format is OKF v0.2. index.md has only `okf_version: "0.2"` frontmatter and "# <category>" sections listing "* [title](path) - description". A superseded memory gets `status: deprecated` and supersededBy, and is rewritten whenever supersession changes.
- Default: a new RecallDb scope given a targetPath mirrors unless the request sends filesystemMirror: false (storage.mirrorByDefault, env NOTDORY_MIRROR_BY_DEFAULT). NotDory never picks a directory itself; a mirror with no targetPath is a 400.
- session_start takes `path` (the repository root). A scope it creates mirrors to <path>/.okf when the server can see that directory and .okf is absent or empty. Otherwise the response notice explains why and how to turn it on.
- A targetPath starting with '~' is rejected (400), because .NET doesn't expand it. It used to create a literal "~" directory in the server's working directory.
- RecallDB is the system of record: search, chat, and store keys come from it. The mirror writes a Memory.ShallowCopy() because the OKF store overwrites StoreKey. A write fails if either side fails. Mirror writes are serialized per bundle path.
- DeleteScope leaves the mirror files. DeleteTenant and Search use the primary only.
- Backfill: a PUT /scopes/{id} that turns the mirror on or changes targetPath calls MemoryService.SyncFilesystemMirrorAsync.
- The DB column is `filesystemmirror` (Migration012). The targetPath is on the server host; in Docker it must be bind-mounted into notdory-server. A remote server can't reach a developer's checkout; see [[okf-mirror-in-repo]].
