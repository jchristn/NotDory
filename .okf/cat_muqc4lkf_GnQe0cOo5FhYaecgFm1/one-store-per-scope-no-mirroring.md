---
type: "Project"
title: "A scope writes to exactly one store; no dual-write or cross-scope search"
description: "MemoryStoreFactory picks one IMemoryStore per scope; no mirroring to Filesystem + RecallDB, and memory_search takes a single scopeId."
timestamp: "2026-10-02T03:24:40.1128480Z"
status: "deprecated"
created: "2026-10-02T03:24:39.2723640Z"
slug: "one-store-per-scope-no-mirroring"
category: "cat_muqc4lkf_GnQe0cOo5FhYaecgFm1"
links: ["memory-store-providers", "docker-stack"]
supersededBy: "mem_muqf0xhb_JTTq3qWpdyyIGthgJav"
version: "1"
salience: "0.5"
---
Verified 2026-10-01: Scope.StoreProvider is a single enum value, and MemoryStoreFactory.Create(scope) returns one store (RecallDb, Verbex, or Filesystem). There is no composite, mirroring, or replication store, and memory_search / chat take one scopeId with no multi-scope fan-out. To get both repo files and semantic search today, use two scopes (one Filesystem with targetPath, one RecallDb) and write to each separately. A Filesystem scope's targetPath is resolved on the NotDory server host: in Docker, docker/compose.yaml mounts only config and logs into notdory-server, so a repo path needs an added bind mount. FilesystemMemoryStore.SearchAsync re-reads the *.md files from disk on each search, so hand edits to the files are searchable. A possible feature: a composite IMemoryStore that writes to both and searches RecallDB.
