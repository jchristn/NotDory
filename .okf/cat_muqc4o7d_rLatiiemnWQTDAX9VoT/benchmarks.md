---
type: "Reference"
title: "Running the benchmark suite against an isolated stack"
description: "src/Test.Benchmark (retrieval, chat, agent, load, compare, prepare, stub) runs against benchmarks/docker on ports 15432/18600/18700/18720."
timestamp: "2026-10-02T02:20:44.4950270Z"
created: "2026-10-02T02:20:44.0945360Z"
slug: "benchmarks"
category: "cat_muqc4o7d_rLatiiemnWQTDAX9VoT"
links: ["retrieval-pipeline"]
version: "1"
salience: "0.5"
---
Setup: `docker compose -f benchmarks/docker/compose.yaml up -d` (pgvector :15432, RecallDB :18600), `dotnet build src/NotDory.sln -c Release`, `benchmarks/start-bench-server.sh` (REST :18700, Prometheus :19464), `benchmarks/start-bench-mcp.sh` (MCP :18720, agent benchmark only), `ollama pull all-minilm` and `gemma3:4b`. Tear down with `docker compose -f benchmarks/docker/compose.yaml down -v`. It never touches the dev stack.
Commands: retrieval (Hit@1, Recall@k, MRR, nDCG@10), chat (LLM-judged accuracy, abstention, citations), agent (headless Claude Code with vs without NotDory), load, compare (diffs two reports, non-zero exit on regression for CI), prepare (BEIR / LongMemEval conversion), stub (stub embedding server), history.
Datasets: benchmarks/datasets/notdory-live.json, atlas.json, notdory-live-followups.json (committed); SciFact and LongMemEval-S are downloaded. Results: benchmarks/RESULTS.md; published baselines: benchmarks/baselines.json. Run retrieval + compare before and after any search-pipeline change.
