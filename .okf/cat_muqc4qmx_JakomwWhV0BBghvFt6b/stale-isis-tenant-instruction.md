---
type: "Project"
title: "Running tenant's \"Start here\" instruction still says Isis"
description: "ins_mtw23496_mC4eOGSdSxXcMHUcgN1 in ten_default still names the product Isis; Migration011 cannot fix it because it only replaces known seeded texts."
timestamp: "2026-10-02T02:21:39.4120310Z"
created: "2026-10-02T02:21:39.1285190Z"
slug: "stale-isis-tenant-instruction"
category: "cat_muqc4qmx_JakomwWhV0BBghvFt6b"
links: ["product-naming", "schema-migrations"]
version: "1"
salience: "0.5"
---
Observed 2026-10-01: session_start on the local deployment returned the tenant-global "Start here" instruction (id ins_mtw23496_mC4eOGSdSxXcMHUcgN1) reading "memory in Isis... the product is 'Isis'... never write it as the all-caps 'ISIS'". The repo source has no remaining "Isis" text, so this is stale seeded data from before the rename (98f27fa). Migration011SessionStartInstructions only replaces content matching its OriginalStartHere list, which holds NotDory-named texts, so the Isis-era text is never replaced. Fix options: edit the instruction in the dashboard or via instruction_update, or add the Isis-era seeded text(s) to the migration's original-text list in a new migration so other deployments are fixed too.
