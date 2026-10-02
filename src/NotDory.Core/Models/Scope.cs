namespace NotDory.Core.Models
{
    using System;
    using NotDory.Core.Enums;
    using NotDory.Core.Helpers;

    /// <summary>
    /// A named memory space within a tenant (a project, a book, or "global"). Maps to one memory store
    /// backend and, for RecallDB, one collection with a fixed embedding dimension.
    /// </summary>
    public class Scope
    {
        #region Public-Members

        /// <summary>
        /// Scope identifier. Defaults to a generated value; may not be set to null or empty.
        /// </summary>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Owning tenant identifier. May not be set to null or empty.
        /// </summary>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>
        /// Human-readable scope name. Unique within a tenant. May not be set to null or empty.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Optional description of what this scope holds.
        /// </summary>
        public string? Description
        {
            get
            {
                return _Description;
            }
            set
            {
                _Description = InputGuard.MaxLength(value, 4096, nameof(Description));
            }
        }

        /// <summary>
        /// The memory store backend for this scope. Determines available retrieval capabilities.
        /// </summary>
        public StoreProviderEnum StoreProvider
        {
            get
            {
                return _StoreProvider;
            }
            set
            {
                _StoreProvider = InputGuard.Defined(value, StoreProviderEnum.RecallDb);
            }
        }

        /// <summary>
        /// For the RecallDB store, the backing collection identifier.
        /// </summary>
        public string? RecallCollectionId { get; set; } = null;

        /// <summary>
        /// The embedding vector dimensionality fixed for this scope (RecallDB). Zero when not applicable.
        /// Changing this after creation requires a new collection and re-embedding.
        /// </summary>
        public int Dimensionality
        {
            get
            {
                return _Dimensionality;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(Dimensionality), "Dimensionality may not be negative.");
                _Dimensionality = value;
            }
        }

        /// <summary>
        /// Identifier of the embedding endpoint used for this scope (RecallDB), if any.
        /// </summary>
        public string? EmbeddingEndpointId { get; set; } = null;

        /// <summary>
        /// Identifier of a rerank endpoint (rep_ id) used to rerank this scope's search results, if any. When set,
        /// searches rerank by default (a query can opt out with <c>rerank: false</c>).
        /// </summary>
        public string? RerankEndpointId { get; set; } = null;

        /// <summary>
        /// The inference endpoint that answers chat in this scope. Null uses the request's endpoint, then the tenant's
        /// first active inference endpoint.
        /// </summary>
        public string? InferenceEndpointId
        {
            get
            {
                return _InferenceEndpointId;
            }
            set
            {
                _InferenceEndpointId = InputGuard.MaxLength(string.IsNullOrWhiteSpace(value) ? null : value.Trim(), 64, nameof(InferenceEndpointId));
            }
        }

        /// <summary>
        /// The inference endpoint that rewrites follow-up questions, splits multi-part questions, and expands queries in
        /// this scope. Null uses <see cref="InferenceEndpointId"/>, then the chat model in use, then the tenant's first
        /// active inference endpoint.
        /// </summary>
        public string? QueryEndpointId
        {
            get
            {
                return _QueryEndpointId;
            }
            set
            {
                _QueryEndpointId = InputGuard.MaxLength(string.IsNullOrWhiteSpace(value) ? null : value.Trim(), 64, nameof(QueryEndpointId));
            }
        }

        /// <summary>
        /// Whether chat rewrites a follow-up question into a standalone query before retrieval. Null uses the server
        /// default (<c>retrieval.chatConversationRewrite</c>).
        /// </summary>
        public bool? ConversationRewrite { get; set; } = null;

        /// <summary>
        /// When searches and chat expand the query with a drafted answer and keywords: Off, On, or Auto (when the search
        /// is not reranked). Null uses the server default (<c>retrieval.queryExpansion</c>, Auto).
        /// </summary>
        public QueryExpansionModeEnum? QueryExpansion
        {
            get
            {
                return _QueryExpansion;
            }
            set
            {
                _QueryExpansion = value.HasValue && Enum.IsDefined(value.Value) ? value : null;
            }
        }

        /// <summary>
        /// Whether searches and chat split a multi-part question into sub-queries. Null uses the server default
        /// (<c>retrieval.queryDecomposition</c>).
        /// </summary>
        public bool? QueryDecomposition { get; set; } = null;

        /// <summary>
        /// How many retrieved candidates are sent to the reranker before the top results are kept. Minimum 1,
        /// maximum 100, default 10. Never fewer than the query's topK.
        /// </summary>
        public int RerankCandidates
        {
            get
            {
                return _RerankCandidates;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(RerankCandidates), "RerankCandidates must be at least 1.");
                if (value > 100) throw new ArgumentOutOfRangeException(nameof(RerankCandidates), "RerankCandidates may not exceed 100.");
                _RerankCandidates = value;
            }
        }

        /// <summary>
        /// Default minimum rerank score for this scope's searches: reranked hits scoring below it are dropped, so a
        /// question with no relevant memory returns nothing instead of the least-bad matches. Null (the default)
        /// keeps every reranked hit. A query's <c>minRerankScore</c> overrides it.
        /// </summary>
        public double? RerankMinScore
        {
            get
            {
                return _RerankMinScore;
            }
            set
            {
                _RerankMinScore = InputGuard.Clamp(value, 0.0, 1.0);
            }
        }

        /// <summary>
        /// For the filesystem store, the layout mode.
        /// </summary>
        public FilesystemLayoutEnum FilesystemLayout
        {
            get
            {
                return _FilesystemLayout;
            }
            set
            {
                _FilesystemLayout = InputGuard.Defined(value, FilesystemLayoutEnum.Hierarchy);
            }
        }

        /// <summary>
        /// For the filesystem store, the target path where memory files are written.
        /// </summary>
        public string? TargetPath
        {
            get
            {
                return _TargetPath;
            }
            set
            {
                _TargetPath = InputGuard.MaxLength(value, 1024, nameof(TargetPath));
            }
        }

        /// <summary>
        /// When memory bodies in this scope are chunked for embedding. Default OnOverflow (only when a body
        /// exceeds the embedding model's token budget).
        /// </summary>
        public ChunkingModeEnum ChunkingMode
        {
            get
            {
                return _ChunkingMode;
            }
            set
            {
                _ChunkingMode = InputGuard.Defined(value, ChunkingModeEnum.OnOverflow);
            }
        }

        /// <summary>
        /// The chunking strategy name (maps to the chunking library's strategy, e.g. FixedTokenCount, Recursive,
        /// SentenceBased, ParagraphBased). Default FixedTokenCount, which packs each chunk to the token budget
        /// with the configured overlap — the predictable choice for embedding regardless of body structure.
        /// </summary>
        public string ChunkStrategy
        {
            get
            {
                return _ChunkStrategy;
            }
            set
            {
                _ChunkStrategy = string.IsNullOrWhiteSpace(value) ? "FixedTokenCount" : InputGuard.MaxLength(value.Trim(), 64, nameof(ChunkStrategy))!;
            }
        }

        /// <summary>
        /// The per-chunk token budget. Zero means use the embedding endpoint's resolved input budget.
        /// </summary>
        public int ChunkMaxTokens
        {
            get
            {
                return _ChunkMaxTokens;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(ChunkMaxTokens), "ChunkMaxTokens may not be negative.");
                _ChunkMaxTokens = Math.Min(value, 8192);
            }
        }

        /// <summary>
        /// The number of tokens of overlap between adjacent chunks. Default 64.
        /// </summary>
        public int ChunkOverlapTokens
        {
            get
            {
                return _ChunkOverlapTokens;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(ChunkOverlapTokens), "ChunkOverlapTokens may not be negative.");
                _ChunkOverlapTokens = Math.Min(value, 1024);
            }
        }

        /// <summary>
        /// Indicates whether the scope is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// UTC timestamp when the scope was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the scope was last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private QueryExpansionModeEnum? _QueryExpansion = null;
        private string? _QueryEndpointId = null;
        private string? _InferenceEndpointId = null;
        private string _ChunkStrategy = "FixedTokenCount";
        private ChunkingModeEnum _ChunkingMode = ChunkingModeEnum.OnOverflow;
        private FilesystemLayoutEnum _FilesystemLayout = FilesystemLayoutEnum.Hierarchy;
        private StoreProviderEnum _StoreProvider = StoreProviderEnum.RecallDb;
        private string? _TargetPath = null;
        private double? _RerankMinScore = null;
        private string? _Description = null;
        private string _Id = IdGenerator.Scope();
        private string _TenantId = String.Empty;
        private string _Name = String.Empty;
        private int _Dimensionality = 0;
        private int _ChunkMaxTokens = 0;
        private int _ChunkOverlapTokens = 64;
        private int _RerankCandidates = 10;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a scope.
        /// </summary>
        public Scope()
        {
        }

        #endregion
    }
}
