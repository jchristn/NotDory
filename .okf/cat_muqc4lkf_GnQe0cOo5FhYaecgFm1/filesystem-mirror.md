---
type: "Project"
title: "RecallDb scopes can mirror every memory to an OKF bundle (filesystemMirror)"
description: "Scope.FilesystemMirror + TargetPath: MirroredMemoryStore writes RecallDB and an OKF bundle concurrently; search uses RecallDB only."
timestamp: "2026-10-02T03:40:41.2912200Z"
created: "2026-10-02T03:40:38.3992040Z"
slug: "filesystem-mirror"
category: "cat_muqc4lkf_GnQe0cOo5FhYaecgFm1"
links: ["memory-store-providers", "schema-migrations"]
version: "1"
salience: "0.5"
---
Added 2026-10-01. A RecallDb scope with `filesystemMirror: true` and a `targetPath` is wrapped by MemoryStoreFactory in MirroredMemoryStore (src/NotDory.Core/Stores/MirroredMemoryStore.cs): upsert/delete go to RecallDB and, concurrently (Task.WhenAll), to a FilesystemMemoryStore in the OkfBundle layout at targetPath (always OKF, regardless of scope.FilesystemLayout).
- RecallDB is the system of record; search, chat, and store keys come from it. The mirror writes a Memory.ShallowCopy() because the OKF store overwrites StoreKey.
- A write fails if either side fails. Mirror writes are serialized per full target path (static SemaphoreSlim map) because each OKF write regenerates index.md.
- DeleteScope leaves the mirror files (the path is often a repo); DeleteTenant and Search use the primary only.
- Validation: ScopeProvisioner.ValidateStorage (create, and the PUT route) returns 400 for a mirror on a Filesystem scope, a missing targetPath, or a path it cannot create (it creates the directory).
- Backfill: PUT /scopes/{id} that turns the mirror on or changes targetPath calls MemoryService.SyncFilesystemMirrorAsync, which re-reads each memory from the NotDory DB under its per-memory lock and writes it to the mirror.
- DB column `filesystemmirror` (Migration012, "2026-10-01-scope-filesystem-mirror"). MCP scope_create/scope_update take filesystemMirror and targetPath. The targetPath is on the server host; in Docker it must be bind-mounted into notdory-server.
