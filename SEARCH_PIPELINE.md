# Search pipeline

How Isis stores memories for retrieval and finds them again: every component on the write path and the read path, in
the order a request meets them, with the reason each step exists, how it is implemented, and where it lives. Measured
effects come from the benchmark suite ([benchmarks/RESULTS.md](benchmarks/RESULTS.md)); the ranked list of changes
still to make is in [RETRIEVAL_IMPROVEMENTS.md](archive/RETRIEVAL_IMPROVEMENTS.md).

## Contents

1. [Architecture at a glance](#1-architecture-at-a-glance)
2. [Write path](#2-write-path)
3. [Read path](#3-read-path)
4. [Chat grounding](#4-chat-grounding)
5. [Other stores](#5-other-stores)
6. [Settings reference](#6-settings-reference)
7. [Input validation](#7-input-validation)
8. [What each stage is worth](#8-what-each-stage-is-worth)
9. [Known limits](#9-known-limits)

---

## 1. Architecture at a glance

Isis splits responsibility between its own relational database and a pluggable retrieval store:

| Component | Owns | Code |
|---|---|---|
| Isis database (SQLite, PostgreSQL, MySQL, or SQL Server) | The memory index: slug, title, summary, full body, category, links, supersession, and the key into the store | `src/Isis.Core/Database` |
| Retrieval store (`IMemoryStore`) | What search runs over: chunk text, vectors, and labels | `src/Isis.Core/Stores` |
| RecallDB (the default store) | Chunk documents with embeddings in PostgreSQL and pgvector; vector and full-text search | `src/Isis.Core/Stores/RecallDb` |
| Model endpoints | Embedding, rerank, and inference models, addressed by base URL with generic authentication | `src/Isis.Core/Recall` |
| `MemoryService` | The write path and the search pipeline around the store | `src/Isis.Server/Services/MemoryService.cs` |
| `MemoryChatService` | Chat: builds grounding context from search and asks the inference model | `src/Isis.Server/Services/MemoryChatService.cs` |

RecallDB is bring-your-own-vector: it stores and searches vectors but never computes them. Isis computes every
embedding through the scope's embedding endpoint and passes the vector in, so the embedding model is a per-scope choice
and RecallDB stays model-agnostic.

```
WRITE   upsert ─► index row (Isis DB) ─► chunk ─► header ─► embed (task prefix) ─► RecallDB documents
                                                                   └─► similar-memory check

READ    query ─► prepare (category, reranker, candidate pool, fusion defaults)
              ─► extra queries (caller's, decomposed parts, expansion forms, chat follow-up rewrite)
              ─► for each query: vector leg ║ text leg ─► weighted RRF + recency ─► collapse chunks to memories
              ─► weighted multi-query fusion ─► min score ─► rerank ─► min rerank score ─► diversity
              ─► snippet trim ─► resolve to memories ─► supersession ─► link expansion ─► hits

CHAT    question (+ history) ─► READ with chat defaults ─► grounding context ─► inference model ─► answer + citations
```

The surfaces are thin over this pipeline: `POST /v1.0/api/tenants/{tid}/scopes/{sid}/memories/search` (REST), the
`memory_search` MCP tool (a proxy to the REST route), and chat (`POST .../chat`, `.../chat/stream`, the MCP `chat` tool).

---

## 2. Write path

### 2.1 Upsert and the index row

**Rationale.** Agents rewrite facts. An upsert is idempotent on (scope, category, slug), so re-writing a slug updates
the memory in place instead of adding a near-duplicate that would later compete with it in search.

**Implementation.** `MemoryService.UpsertAsync` sanitizes the text (invalid surrogate pairs), takes a per-memory lock
(two writers to the same slug serialize), writes the index row, then writes the store. The full body lives in the Isis
database, so search results can be resolved back to whole memories (section 3.10).

### 2.2 Chunking

**Rationale.** Embedding models have a small input limit (all-minilm: 256 tokens), and one vector for a long memory
blurs its details. Chunking keeps every part of a memory searchable. Round 4 showed the chunk size matters more than
expected: chunks that fill the model window dilute details, and chunks sized for retrieval score better (Atlas 0.802 to
0.831 when chunks dropped from about 240 to about 190 tokens).

**Implementation.** `MemoryChunker` (`src/Isis.Core/Recall/MemoryChunker.cs`) uses TextChunker with local token
counting, so no model call is needed to size a chunk.

- The budget comes from the embedding endpoint: its `MaxInputTokens`, or, when 0, the model's known limit. A 1% margin
  (`TokenizerMarginFraction`) covers small tokenizer differences, and BERT framing tokens are reserved.
- Chunk size, unless the scope sets `chunkMaxTokens`: 75% of the budget (`DefaultChunkFraction`), capped at 256 tokens
  (`DefaultChunkMaxTokens`). An embedding model profile can override both; nomic-embed-text uses a 128-token cap,
  chosen by sweep.
- The scope's chunking mode decides whether short memories are chunked at all (`OnOverflow` chunks only bodies over the
  budget).

### 2.3 Chunk headers

**Rationale.** A chunk from the middle of a long memory ("the TTL is five minutes") often lacks the words that say what
it is about. Prefixing each chunk with its memory's title and summary gives every chunk that context.

**Implementation.** `MemoryService.ChunkHeader` builds `title: summary`. The chunker reserves the header's tokens from
each chunk's budget and truncates a header longer than 25% of it (`HeaderBudgetFraction`), so the body always keeps
most of the chunk. The header is embedded; it is not stored as chunk text.

**Trade-off.** Headers helped paraphrase and multi-memory questions but cost exact-identifier questions on Atlas (0.833
to 0.740 in round 2), since header words compete with identifiers. The reranker recovers most of that (0.911 on Atlas
lexical questions with the cross-encoder).

### 2.4 Embedding

**Rationale.** One embedding call per chunk, reliably, against endpoints that throttle.

**Implementation.** `EmbeddingService` (`src/Isis.Core/Recall/EmbeddingService.cs`).

- **Task prefixes.** Models trained with instruction prefixes get them: nomic-embed-text `search_document: ` and
  `search_query: `, e5 `passage: ` and `query: `, and the BGE, mxbai, and snowflake-arctic-embed query prefixes. They
  come from `EmbeddingModelProfiles`, which holds generic defaults plus overrides only where a benchmark showed a known
  model needs one, so a new model works without code changes.
- **Parallelism.** A memory's chunks are embedded concurrently, up to `retrieval.embeddingParallelism` (default 4).
- **Retries.** `TransientRetryHandler` retries 429, 502, and 503 with backoff. An endpoint still unavailable afterwards
  raises `ModelEndpointUnavailableException`, reported as 503 so callers know to retry, instead of 400.
- **Context-length retry.** If the endpoint still rejects a chunk as too long, the memory is re-chunked at
  progressively smaller fractions of the budget.

### 2.5 Storage in RecallDB

**Rationale.** Chunks must roll back up to one hit per memory at search time, and a re-chunk must never leave stale
chunks behind.

**Implementation.** `RecallDbMemoryStore.UpsertAsync`. A single-chunk memory is one document keyed by the memory id; a
multi-chunk memory's documents are keyed `{memoryId}-c{ordinal}`, share the slug as document id, and carry a
`parentKey` tag and the ordinal as position. The category is a label, so category filters run in the store. Existing
documents for the memory are deleted first. Each scope is one RecallDB collection with a fixed dimension, created on
first use under a per-tenant lock.

### 2.6 Similar memories on upsert

**Rationale.** Search quality depends on the memory set: two memories stating different versions of a fact confuse
every later search. Telling the writer at write time is cheaper than resolving it at read time.

**Implementation.** After an upsert on a semantic store, a vector search with the new memory's first chunk returns up
to 3 existing memories at cosine similarity 0.85 or more (`retrieval.duplicateSimilarityThreshold`) as
`similarMemories`. The writer can reuse the slug or supersede the old memory. These are prompts, not decisions.

### 2.7 Supersession on write

**Rationale.** "The deploy target moved from A to B" should not leave A ranking as the current fact.

**Implementation.** An upsert can name the memories it replaces (`supersedes`). The server records `supersededBy` on
each replaced memory. Search applies it in section 3.10. On Atlas this took superseded-fact questions from 0.718 to
0.876.

---

## 3. Read path

### 3.1 Validation and category filter

**Rationale.** Agents pass category names; the store labels documents with category ids. A name that silently matched
nothing would look like an empty memory.

**Implementation.** An empty `queryText` is rejected (400). `ResolveCategoryFilterAsync` accepts a name or a `cat_` id
and resolves it to the id; an unknown category is an error, not an empty result.

### 3.2 Reranker resolution and circuit breaker

**Rationale.** A reranker is the largest measured quality lever (section 7), but a reranker outage must not fail or
slow every search.

**Implementation.** The rerank endpoint is the scope's `rerankEndpointId` unless the search sets `rerank: false`. New
RecallDB scopes attach the tenant's first active cross-encoder (an inference endpoint with the `Tei` or `Cohere` format)
automatically, never a chat model, and the reference stack seeds one when `ISIS_DEFAULT_RERANK_BASEURL` is set. After a rerank failure, searches skip that endpoint for 30 seconds
(`RerankCooldown`) and return retrieval order with a notice.

### 3.3 Candidate pool

**Rationale.** Reranking and diversity choose among more candidates than they return.

**Implementation.** With a reranker, the store retrieves `max(topK, rerankCandidates)` (default 10) and each candidate
carries up to 1,200 characters for the reranker (`RerankPassageChars`) instead of the caller's snippet budget. With
diversity on, the pool widens to `min(100, topK x 3)`. Ten candidates matched twenty in quality at about 40% less
latency (round 5).

### 3.4 Fusion defaults

**Rationale.** The right text weight and RRF constant could depend on the embedding model; keeping that out of callers
keeps the API simple.

**Implementation.** A search that leaves `textWeight` or `rrfK` null gets the embedding model profile's value, then the
generic default: text weight 0.5, RRF constant 20. A sweep over four datasets and two models chose these, and no model
needed its own values, so the profiles carry none today.

### 3.5 Extra queries

**Rationale.** One query string is sometimes the wrong query: a multi-part question, a follow-up that depends on the
conversation, or a question phrased unlike the memory that answers it. Searching extra forms and fusing the rankings
addresses each, but only if the extra forms cannot push out what the original query already ranked well. Round 6
showed that failure: decomposed parts fused at full weight lowered every dataset (Atlas 0.831 to 0.764). So every extra
query carries a weight, and the original always counts 1.0.

**Implementation.** `MemoryService.WeightedQueries` builds the list: the main query (weight 1.0, the search's mode), up
to 4 `additionalQueries` (weight `additionalQueryWeight`, default 1.0), then up to 4 `subQueries`, each with its own
weight and optional mode. Sources:

| Source | How | Weight | Mode | Default |
|---|---|---|---|---|
| Caller | `additionalQueries`, `subQueries` | as given | as given | none |
| Decomposition | `decompose`, else the scope's `queryDecomposition`: `QueryDecomposer` asks the query model to split a question of 6 or more words into up to 3 self-contained queries | `additionalQueryWeight` | search's mode | off |
| Expansion | `expand`, else the scope's `queryExpansion`: `QueryExpander` asks for a 1 to 3 sentence hypothetical answer and up to 8 keywords | `expansionWeight` (default 0.5) | answer by vector (Semantic), keywords as text (Keyword) | `Auto`: on for searches that are not reranked (it helps the public datasets, is neutral on agent memory, costs one model call, and adds nothing on top of a cross-encoder) |
| Chat follow-up | Chat with `history`: `ConversationRewriter` rewrites the latest message as a standalone query | 1.0 | search's mode | on in chat |

The model for all three is the scope's query model (section 3.11). All three use one model-agnostic prompt, strip `<think>` blocks, parse a strict format, time out
after 20 seconds, and fall back to the original query on any failure. Expansion targets the two legs separately
because each form suits one: a drafted answer reads like a stored memory, so it embeds near the answering memory,
while keywords (including exact identifiers from the question) match the full-text leg.

### 3.6 Retrieval per query (RecallDB)

**Rationale.** Keyword and vector search fail differently: vectors miss exact identifiers and rare terms, full text
misses paraphrase. Hybrid search runs both and fuses them. RecallDB's combined query treated the text query as a
required filter, which dropped strong vector matches that shared no keyword with the question, so Isis runs the two
legs separately.

**Implementation.** `RecallDbMemoryStore.SearchAsync`, `HybridFusion.Fuse`.

1. **Two legs, concurrently.** A vector search (cosine similarity) and a full-text search (any-term matching, ranked),
   each for `max(topK x 4, 20)` chunk documents, with the category label filter. Fetching well beyond topK leaves
   enough distinct memories after chunks collapse. Latency is the slower leg, not the sum.
2. **Weighted reciprocal-rank fusion.** Each chunk scores `(1 - w) / (k + vectorRank) + w / (k + textRank)`, with text
   weight `w` (0.5) and constant `k` (20). RRF uses ranks, not raw scores, because cosine similarity and text rank are
   on unrelated scales.
3. **Recency.** A third RRF term, `r / (k + recencyRank)`, ranks each chunk's parent memory by write time (newest
   first). `recencyWeight` defaults to 0.1, enough to prefer the newer of two near-equal memories without overriding
   relevance; bulk imports of old material should use 0.
4. **Normalization.** Scores are divided by the best possible score, so they lie in 0 to 1 and `minScore` means the
   same thing across queries. Each hit keeps its evidence: `vectorScore`, `textScore`, `vectorRank`, `textRank`.
5. **Collapse.** Chunks roll up to one hit per memory (`GroupByParent`), keeping the best chunk as the snippet.

Semantic and Keyword modes run one leg and skip fusion.

**Single call.** When the RecallDB server reports the `search.hybrid.rrf` and `search.collapse` capabilities (checked
once per client), steps 1 to 5 run inside RecallDB in one request: Isis sends its text weight, RRF constant, candidate
pool, and recency weight, and RecallDB collapses chunks by the `parentKey` tag. The two paths return the same rankings
(identical nDCG@10 on all four benchmark datasets); the single call saves a request and the client-side fusion. Older
servers, a failed call, or `retrieval.serverSideHybrid: false` use the two-call path.

### 3.7 Multi-query fusion

**Rationale.** The rankings from section 3.5's queries must become one ranking, and the weights decide how much an
extra query can change it. The RRF constant matters as much as the weights: at the conventional k = 60, the main
query's first and tenth hits differ by only 0.002, while the top hit of a 0.3-weight extra query adds 0.005, so any
extra query could reorder the whole top ten regardless of its weight. At k = 5 the main query's tenth hit (1/15)
still outscores a 0.3-weight query's first hit (0.3/6), so extra queries mostly re-rank the main query's candidates,
lifting the ones they agree on, instead of displacing them.

**Implementation.** `MemoryService.FuseQueryResults` (weighted RRF, k = 5, `retrieval.queryFusionRrfK`): a memory at
rank r in a ranking of weight w adds `w / (k + r)`. Scores are normalized by the total weight, so a memory ranked first by every
query scores 1.0. Each memory keeps the snippet and evidence from the query that ranked it best. With one query this
stage does nothing. The main query's result supplies the effective mode and notices.

### 3.8 Thresholds and reranking

**Rationale.** A cross-encoder reads the query and each candidate together, so it judges relevance far better than
either retrieval leg, at a latency that only allows scoring a short candidate list.

**Implementation.** `RerankService` (`src/Isis.Core/Recall/RerankService.cs`), through PolyPrompt like every other model
call (`ModelClientFactory`).

1. `minScore`, if set, drops candidates below the fused score.
2. The candidates (title plus up to 1,200 characters) and the original query (never an extra query) go to the rerank
   endpoint:
   - **Cross-encoders** (`Tei`, and `Cohere` for Cohere's v2 rerank API or a vLLM-served cross-encoder): PolyPrompt's
     `RerankAsync`, scores in 0 to 1. The reference stack serves `cross-encoder/ms-marco-MiniLM-L-6-v2`.
   - **Chat models** (`Ollama`, `OpenAI`, `VLlm`, `Gemini`): one prompt at temperature 0 rates every candidate 0 to 10,
     divided by 10. Small models rank
     worse than no reranker (gemma3:4b lowered every dataset); large ones rank best of all (gpt-oss-20b, section 7),
     at several seconds per search.
3. Hits are reordered by rerank score and carry `rerankScore`. `minRerankScore` (or the scope's `rerankMinScore`) then
   drops hits below it, so a question with no relevant memory can return nothing.
4. A failure returns retrieval order with a notice and starts the cooldown (section 3.2).

### 3.9 Diversity and snippets

**Rationale.** Near-duplicate memories waste result slots; callers budget how much text they receive.

**Implementation.** `SearchDiversifier` reorders by maximal marginal relevance when `diversity` is above 0 (default 0),
using word-set overlap as the similarity because it works for every store without stored vectors. Snippets are cut to
`tokenBudget` characters (default 240) after reranking, so the reranker saw the longer passage.

### 3.10 Resolution, supersession, and links

**Rationale.** Search runs over chunks in the store; callers need current, whole memories.

**Implementation.** `SearchRefiner` (`src/Isis.Server/Services/SearchRefiner.cs`).

1. **Resolve.** Each hit maps back to its memory row by store key, since a slug is only unique per category.
2. **Supersession** (`superseded`): `Demote` (default) moves a replaced memory to directly after its replacement,
   adding the replacement when the search missed it; `Hide` drops replaced memories, also adding a missed replacement;
   `Include` only marks them.
   Replacement chains are followed to the current memory, and hits carry `supersededBy`.
3. **Link expansion** (`linkExpansion`, 0 to 10, default 0): memories linked from a result, through `links` or
   `[[slug]]` references in the body, are added directly after it with `linkedFrom`. Off for search because it lowered
   isis-live nDCG (0.877 to 0.852), on in chat (2) where the extra context helps answers.
4. The list is cut to `topK`.

### 3.11 Per-scope models and query steps

**Rationale.** Which model does which job is a deployment choice, not something Isis should hard-code: a team may
answer chat with a large model but rewrite queries with a small fast one, or rerank with a large chat model where
precision matters more than latency. `QueryPreparer` puts that in the scope, in the same shape as an assistant's
settings in AssistantHub.

**Implementation.** `src/Isis.Server/Services/QueryPreparer.cs`, used by both search and chat.

| Job | Scope field | Resolution |
|---|---|---|
| Embedding | `embeddingEndpointId` | set at creation |
| Reranking | `rerankEndpointId`, `rerankCandidates`, `rerankMinScore` | set at creation (tenant's first cross-encoder); any inference endpoint whose format can rerank |
| Chat answers | `inferenceEndpointId` | request, then scope, then tenant's first active inference endpoint |
| Query steps | `queryEndpointId` | request, then scope, then the scope's chat model, then the chat model in use, then tenant default |
| Follow-up rewrite | `conversationRewrite` | scope, then `retrieval.chatConversationRewrite` (true) |
| Expansion | `queryExpansion` | request `expand`, then scope, then `retrieval.queryExpansion` (`Auto`) |
| Decomposition | `queryDecomposition` | request `decompose`, then scope, then `retrieval.queryDecomposition` (false) |

Every model other than embedding is an inference endpoint; its API format decides which jobs it can take
(`ApiFormatCapabilities`): chat formats answer chat, run query steps, and rerank by prompt;
`Tei` and `Cohere` cross-encoders only rerank. A named endpoint that is inactive or cannot do the job is skipped in
favor of the next choice; scope create and update reject one that is missing or cannot do the job. The dashboard's scope form exposes every field.

---

## 4. Chat grounding

**Rationale.** Chat answers from memories, not from the model's general knowledge, and must say so when memory does
not hold the answer.

**Implementation.** `MemoryChatService.BuildContextAsync`.

1. **Keyword-only stores** (filesystem) skip search: the model receives every memory (up to 200), grouped by category,
   because lexical ranking fails on broad or meta questions and these stores are small.
2. **Query forms.** With `history`, the follow-up rewrite runs first (section 3.5), then decomposition and expansion
   when the scope's settings call for them (section 3.11), all on the scope's query model.
3. **Search** in Hybrid mode for 8 memories (`DefaultTopK`), with up to 4,000 characters of the best chunk per memory
   (a whole memory for typical sizes) and link expansion 2. Grounding on a 240-character snippet was the largest chat
   defect found: accuracy rose from 0.68 to 0.96 when chat moved to whole chunks.
4. **Context.** Each memory is listed with its slug, title, and text; replaced memories are labelled outdated and
   linked ones say where they came from.
5. **No hits.** If the reranker rejected every candidate, the model gets no memories and says the answer is not in
   memory. If search simply matched nothing, it gets the scope overview.
6. **Prompt.** The system prompt restricts answers to stated facts, requires a slug citation for every claim, and
   requires a plain "not in memory" answer otherwise. With history, the recent conversation (up to 6 messages of 1,000
   characters) precedes the question, with the standalone form noted.

---

## 5. Other stores

| Store | Search | Notes |
|---|---|---|
| RecallDB (default) | Vector, full text, hybrid | Everything in section 3 |
| Filesystem | Keyword only | Memories as files (single file, hierarchy, or OKF bundle), trackable in git. Extra queries run as keyword searches; supersession and links apply, and reranking does when the scope names a reranker. Chat uses the overview (section 4) |
| Verbex | Keyword (TF-IDF) | Not wired: creating a Verbex scope is rejected with 400, and searches raise `NotSupported` |

---

## 6. Settings reference

| Setting | Where | Default | Section |
|---|---|---|---|
| `chunkMaxTokens`, `chunkingMode` | Scope | model-derived, `OnOverflow` | 2.2 |
| `DefaultChunkFraction`, `DefaultChunkMaxTokens` | `MemoryChunker` | 0.75, 256 | 2.2 |
| `HeaderBudgetFraction` | `MemoryChunker` | 0.25 | 2.3 |
| `retrieval.embeddingParallelism` | Server settings | 4 | 2.4 |
| `retrieval.duplicateSimilarityThreshold` | Server settings | 0.85 | 2.6 |
| `rerankEndpointId`, `rerankCandidates`, `rerankMinScore` | Scope | auto-attached, 10, none | 3.2, 3.8 |
| `RerankCooldown`, `RerankPassageChars` | `MemoryService` | 30 s, 1,200 | 3.2, 3.3 |
| `topK` | Search | 10 | 3.6 |
| `textWeight`, `rrfK` | Search, model profile | 0.5, 20 | 3.4, 3.6 |
| `recencyWeight` | Search | 0.1 | 3.6 |
| `additionalQueries`, `subQueries` | Search | none (at most 4 each) | 3.5 |
| `additionalQueryWeight` | Search, `retrieval.additionalQueryWeight` | 1.0 | 3.5 |
| `decompose`, `expand`, `expansionWeight` | Search, `retrieval.expansionWeight` | false, false, 0.5 | 3.5 |
| `retrieval.queryFusionRrfK` | Server settings | 5 | 3.7 |
| `minScore`, `minRerankScore`, `rerank` | Search | none, scope's, scope's | 3.8 |
| `diversity`, `tokenBudget` | Search | 0, 240 | 3.9 |
| `superseded`, `linkExpansion` | Search | `Demote`, 0 | 3.10 |
| `retrieval.chatLinkExpansion` | Server settings | 2 | 4 |
| `retrieval.chatConversationRewrite`, `retrieval.chatHistoryTurns` | Server settings | true, 6 | 4 |
| `retrieval.queryDecomposition`, `retrieval.queryExpansion` | Server settings | false, `Auto` | 3.5, 3.11 |
| `inferenceEndpointId`, `queryEndpointId`, `conversationRewrite`, `queryExpansion`, `queryDecomposition` | Scope | null (server defaults) | 3.11 |
| `retrieval.serverSideHybrid` | Server settings | true | 3.6 |
| `cache.enabled`, `cache.ttlSeconds` | Server settings | true, 10 | lookups for every request |

---

## 7. Input validation

Every value that reaches the pipeline is checked where it enters (`InputGuard` in `src/Isis.Core/Helpers`, applied in
model and settings setters): numbers clamp to a valid range with NaN and infinity falling back to the default, text and
lists over a limit are rejected with 400, null lists become empty, undefined enum values fall back, and request bodies
over 16 MB are rejected with 413. The limits that matter for search and chat: `queryText` 4,000 characters, at most 8
additional queries and 8 sub-queries, `tokenBudget` 16 to 20,000, chat `question` 8,000 characters, and chat
`history` 100 messages. The full table is in [docs/REST_API.md](docs/REST_API.md#input-limits).

## 8. What each stage is worth

Hybrid nDCG@10 unless noted, from [benchmarks/RESULTS.md](benchmarks/RESULTS.md). isis-live is 24 real memories and
110 questions; Atlas is 170 synthetic memories and 260 questions; SciFact and LongMemEval are public datasets.

| Stage | Evidence |
|---|---|
| Keyword search matching any term (RecallDB fix) | SciFact Keyword 0.057 to 0.598; Hybrid improved on every dataset |
| Hybrid over either leg alone | isis-live 0.878 vs 0.812 Keyword and 0.826 Semantic; SciFact 0.683 vs 0.594 and 0.658 |
| Round 2 retrieval changes (fused, normalized scores; recency; chunk headers) | isis-live 0.859 to 0.878 |
| Retrieval-sized chunks (75% of budget, 256 cap) | Atlas 0.802 to 0.831, LongMemEval 0.895 to 0.912 |
| RRF constant 20 instead of 60 | Mean over four datasets 0.826 to 0.827 (all-minilm), 0.808 to 0.815 (nomic) |
| Supersession | Atlas superseded-fact questions 0.718 to 0.876 |
| Cross-encoder reranker (10 candidates) | isis-live 0.878 to 0.925, Atlas 0.835 to 0.883, SciFact 0.683 to 0.712, LongMemEval 0.911 to 0.939; 0.3 to 0.4 s per search on CPU |
| gpt-oss-20b as reranker | 0.974, 0.915, 0.751, 0.959; answerable vs unanswerable AUROC 0.96 to 0.99; 7 to 10 s per search |
| Whole-chunk chat grounding | Chat accuracy 0.68 to 0.96 |
| Chat follow-up rewrite | Follow-up set, 3 memories retrieved: evidence in the prompt 0.953 to 1.000, accuracy 0.969 to 1.000 |
| Decomposition | At full weight and k = 60 lowered every dataset (Atlas 0.831 to 0.764); at weight 0.5 and k = 5 neutral (mean 0.826 vs 0.827); off by default |
| Expansion (weight 0.5, k = 5), mean of two runs | isis-live 0.878 to 0.876, Atlas 0.835 to 0.835, SciFact 0.683 to 0.720, LongMemEval 0.911 to 0.932; runs differ by about 0.01 because the drafts vary; about 2 s per search; chat latency 2.1 to 4.1 s with no change in evidence reaching the prompt |
| Single-call hybrid (RecallDB) | Identical nDCG@10 to the two-call path on all four datasets; similar latency |
| Multi-query fusion constant 5 instead of 60 | Expansion's Atlas result 0.816 to 0.842, decomposition's mean 0.804 to 0.826 |

SciFact against published results: Isis Hybrid is +0.018 over BM25 and +0.038 over dense all-MiniLM-L6-v2; with the
cross-encoder it is +0.024 over BM25 with a cross-encoder.

---

## 9. Known limits

- **Diversity by word overlap.** Search results do not carry stored vectors yet, so diversity and the duplicate check
  compare words. RecallDB returns vectors on request now; switching to cosine similarity is planned.
- **No calibrated "nothing relevant" score** without a large reranking model: no single fused, vector, or cross-encoder
  score separates answerable from unanswerable questions well (AUROC 0.57 to 0.79).
- **Model-dependent extra queries.** Decomposition, expansion, and the follow-up rewrite depend on the inference model's
  output; a small model's rewrites are often keyword lists, which still search well because the original query is
  always searched too.
