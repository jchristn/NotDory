---
type: "Project"
title: "Search and chat retrieval pipeline stages"
description: "Parallel vector + full-text, weighted RRF with recency, chunk rollup, optional rerank, supersession, link expansion; documented in SEARCH_PIPELINE.md."
timestamp: "2026-10-02T02:20:05.3659800Z"
created: "2026-10-02T02:20:04.9784040Z"
slug: "retrieval-pipeline"
category: "cat_muqc4lkf_GnQe0cOo5FhYaecgFm1"
links: ["memory-store-providers", "rerankers-are-inference-endpoints", "benchmarks"]
version: "1"
salience: "0.5"
---
A memory_search runs vector and full-text search in parallel, fuses by weighted reciprocal rank with a small recency signal, rolls chunks up to one hit per memory, optionally reranks (cross-encoder or chat model via an inference endpoint, rerankMinScore cutoff), applies supersession (Demote/Hide/Include: replaced memories rank after their replacement), optional diversity (SearchDiversifier) and link expansion. Optional extra queries (additionalQueries up to 4, decompose via QueryDecomposer, expand via QueryExpander drafting a hypothetical answer + keywords) are fused at lower weight.
Chat (MemoryChatService) grounds on the best whole chunk of each retrieved memory, cites every claim, rewrites follow-ups into standalone queries (ConversationRewriter), and abstains when memory lacks the answer.
Each scope picks its models: embeddingEndpointId, rerankEndpointId, inferenceEndpointId (chat), queryEndpointId (query steps) plus conversationRewrite, queryExpansion Off|On|Auto, queryDecomposition; null = server default. Every stage, its rationale and measured effect: SEARCH_PIPELINE.md; history of attempts: archive/RETRIEVAL_IMPROVEMENTS.md.
Measured: hybrid nDCG@10 0.84-0.91 on memory datasets, 0.88-0.94 with cross-encoder.
