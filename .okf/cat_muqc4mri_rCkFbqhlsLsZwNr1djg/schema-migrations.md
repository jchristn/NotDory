---
type: "Project"
title: "How to add a database schema/data migration"
description: "Add Database/Migrations/MigrationNNN<Name>.cs (internal sealed ISchemaMigration, Name = \"yyyy-MM-dd-slug\"), register it in MigrationRunner, and update all 3 setup-query files."
timestamp: "2026-10-02T03:41:03.9402090Z"
created: "2026-10-02T02:21:02.9423430Z"
slug: "schema-migrations"
category: "cat_muqc4mri_rCkFbqhlsLsZwNr1djg"
links: ["csharp-code-style", "changelog-practice", "stale-isis-tenant-instruction"]
version: "2"
salience: "0.5"
---
Migrations live in src/NotDory.Core/Database/Migrations as `MigrationNNN<PascalName>.cs`. The latest is Migration012ScopeFilesystemMirror, so the next is Migration013. Each is an `internal sealed class` implementing ISchemaMigration with `Name => "yyyy-MM-dd-kebab-slug"`. Append the new instance to the ordered list in src/NotDory.Core/Database/MigrationRunner.cs.

A new column must also go into all three CREATE TABLE scripts: Sqlite/Queries/SetupQueries.cs (shared by Sqlite and Postgresql), Mysql/Queries/MysqlSetupQueries.cs, and SqlServer/Queries/SqlServerSetupQueries.cs. It also goes into the Sqlite/Implementations/*Methods.cs INSERT, UPDATE, and FromRow (guarded by row.Table.Columns.Contains). Every driver uses those shared implementations.

Column-adding migrations check existence with `SELECT col FROM t WHERE 1 = 0` and use "ALTER TABLE t ADD " on SQL Server versus "ADD COLUMN " elsewhere. Data migrations that rewrite seeded text only replace rows that still match a known seeded text exactly (see Migration011). Name each migration in its CHANGELOG entry.
