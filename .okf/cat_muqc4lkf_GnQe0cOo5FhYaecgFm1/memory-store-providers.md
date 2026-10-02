---
type: "Project"
title: "Pluggable IMemoryStore providers chosen per scope"
description: "RecallDB (semantic+hybrid, default, optional OKF filesystem mirror) or Filesystem (keyword, git-trackable). Verbex was removed 2026-10-01."
timestamp: "2026-10-02T03:40:45.2090030Z"
created: "2026-10-02T02:19:58.6242140Z"
slug: "memory-store-providers"
category: "cat_muqc4lkf_GnQe0cOo5FhYaecgFm1"
links: ["system-overview", "retrieval-pipeline", "filesystem-mirror"]
version: "2"
salience: "0.5"
---
Memory content is stored through IMemoryStore (src/NotDory.Core/Stores/IMemoryStore.cs: EnsureScopeAsync, UpsertAsync with chunks, DeleteAsync, DeleteScopeAsync, DeleteTenantAsync, SearchAsync), selected per scope by MemoryStoreFactory. StoreProviderEnum has two values: RecallDb and Filesystem.
- RecallDb (default; Stores/RecallDb/RecallDbMemoryStore.cs, HybridFusion.cs): the only provider with semantic and hybrid search. It requires an embedding endpoint, and the scope's embedding model and dimension are fixed at creation. Oversized bodies are chunked, and chunks roll up to one hit per memory. With filesystemMirror it is wrapped in MirroredMemoryStore (see [[filesystem-mirror]]).
- Filesystem (Stores/Filesystem): keyword only and git-trackable, with layouts SingleFile, Hierarchy, and OkfBundle (Open Knowledge Format: one markdown file per memory, root/<categoryId>/<slug>.md with YAML frontmatter, plus a generated index.md). It runs without Docker, and search re-reads the files from disk.
- Verbex was removed entirely on 2026-10-01 (code, settings section, docs). storeProvider "Verbex" is now an unknown enum value and returns 400.
