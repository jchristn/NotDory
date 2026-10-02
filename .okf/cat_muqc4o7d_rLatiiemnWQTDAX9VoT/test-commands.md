---
type: "Reference"
title: "Running the automated test suite (Touchstone)"
description: "dotnet run --project src/Test.Automated -c Release; tests are Touchstone cases registered in src/Test.Shared/NotDorySuites.cs."
timestamp: "2026-10-02T02:20:28.2564740Z"
created: "2026-10-02T02:20:27.9250580Z"
slug: "test-commands"
category: "cat_muqc4o7d_rLatiiemnWQTDAX9VoT"
links: ["build-commands"]
version: "1"
salience: "0.5"
---
Run: `dotnet run --project src/Test.Automated/Test.Automated.csproj -c Release` (test.bat on Windows; there is no test.sh yet). Optional `--results <path>` writes a results file.
The framework is Touchstone (Touchstone.Core / Touchstone.Cli 0.1.12), not xUnit/NUnit. Tests are registered in src/Test.Shared/NotDorySuites.cs GetSuites() via Async(id, name, fn), Sync(...), or Skippable(..., condition, reason). Topic suites live in Test.Shared/*Suite.cs (Auth, Chunker, Database, Install, Mcp, Model, Refinement, Rest, Retrieval, ScopeModels, Service, Store, Validation). Helpers: ServerHarness (in-process server), TempSqlite, FilesystemFixture, stub HTTP handlers for model endpoints, and DockerDb (live-postgresql and similar tests are skipped when Docker is unavailable). Add a new test by writing the method in the matching suite and registering it in GetSuites.
