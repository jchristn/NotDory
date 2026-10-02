---
type: "Project"
title: "Changelog and docs updates accompany every feature"
description: "Add a bolded-lead bullet under \"## [0.1.0] - ALPHA (in progress)\" in CHANGELOG.md and update REST_API.md / MCP_API.md / dashboard / tests together."
timestamp: "2026-10-02T02:21:08.3521710Z"
created: "2026-10-02T02:21:07.9523630Z"
slug: "changelog-practice"
category: "cat_muqc4mri_rCkFbqhlsLsZwNr1djg"
links: ["schema-migrations"]
version: "1"
salience: "0.5"
---
CHANGELOG.md follows SemVer; all current work goes under `## [0.1.0] - ALPHA (in progress)` -> `### Added` (etc.). Each entry is a bullet starting with a bold one-sentence headline (e.g. "**Endpoint reasoning setting.**") followed by prose naming the API fields, REST routes, MCP tools, migrations, and dashboard changes involved. Features are delivered end to end: REST + MCP + dashboard + docs (docs/REST_API.md, docs/MCP_API.md, README.md, SEARCH_PIPELINE.md for retrieval changes) + tests in Test.Shared. Being ALPHA, breaking changes without migration paths are allowed but must be documented.
