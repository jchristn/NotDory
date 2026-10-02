---
type: "Project"
title: "Rerankers are inference endpoints, not a separate endpoint kind"
description: "Endpoint kinds are Embedding and Inference only; ApiFormat decides capability (Tei/Cohere = cross-encoder rerank only; Ollama/OpenAI/VLlm/Gemini chat can rerank by prompt)."
timestamp: "2026-10-02T02:21:24.8623800Z"
created: "2026-10-02T02:21:24.4803830Z"
slug: "rerankers-are-inference-endpoints"
category: "cat_muqc4pnc_EkpVw6agqqeOD8SfT90"
links: ["retrieval-pipeline"]
version: "1"
salience: "0.5"
---
Decision (CHANGELOG 0.1.0, migration 2026-09-27-rerank-endpoints-are-inference / Migration008): the separate Rerank endpoint kind was removed. Every non-embedding model is an Inference endpoint, and the scope chooses which one answers chat (inferenceEndpointId), runs query steps (queryEndpointId), and reranks (rerankEndpointId). Tei and Cohere formats are cross-encoders that can only rerank; chat formats rerank by prompt at temperature 0. "Rerank" is still accepted as a kind on input and stored as Inference. New scopes auto-attach the tenant's first cross-encoder, never a chat model; validation rejects a cross-encoder as a chat or query model. Why: one chat model can serve both jobs without double registration, and all model calls go through PolyPrompt (ModelClientFactory) with shared auth, retry, and timeouts. Per-endpoint `reasoning` (Default|Off|Low|Medium|High) makes thinking models usable as rerankers.
