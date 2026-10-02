---
type: "Project"
title: "NotDory metadata DB is separate from RecallDB content"
description: "NotDory owns a `notdory` DB (Sqlite/Mysql/Postgres/SqlServer) for what RecallDB has no schema for; RecallDB holds content and vectors."
timestamp: "2026-10-02T02:21:33.7750490Z"
created: "2026-10-02T02:21:33.4568120Z"
slug: "split-metadata-and-content-stores"
category: "cat_muqc4pnc_EkpVw6agqqeOD8SfT90"
links: ["system-overview", "memory-store-providers"]
version: "1"
salience: "0.5"
---
RecallDB is the system of record for memory content, embeddings, and retrieval. NotDory keeps a separate metadata database (in Docker, the `notdory` database on the same Postgres as RecallDB; locally Sqlite by default) for categories and instructions, policies, seed packs, the link graph, slugs/titles/summaries, endpoints, tenancy/auth, and request history. NotDory computes embeddings itself and passes vectors to RecallDB, so the embedding model is NotDory's choice per scope. Supported metadata DBs: Sqlite, Mysql, Postgresql, SqlServer (Core/Database drivers).
