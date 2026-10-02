namespace NotDory.Server.Settings
{
    using System;
    using NotDory.Core.Enums;
    using NotDory.Core.Helpers;

    /// <summary>
    /// Server-wide retrieval settings: the similarity check on upsert, reranker input size, and chat link expansion.
    /// Per-scope rerank settings live on the scope.
    /// </summary>
    public class RetrievalSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether an upsert reports existing memories that closely resemble the one written (in
        /// <c>similarMemories</c>). Only scopes with semantic search are checked. Default true.
        /// </summary>
        public bool DuplicateCheckEnabled { get; set; } = true;

        /// <summary>
        /// Minimum vector similarity for an existing memory to be reported as similar, in the range 0.0 to 1.0.
        /// Default 0.85 (with all-minilm, replaced facts score about 0.55 to 0.88 against their replacement).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside [0, 1].</exception>
        public double DuplicateSimilarityThreshold
        {
            get
            {
                return _DuplicateSimilarityThreshold;
            }
            set
            {
                if (!double.IsFinite(value) || value < 0.0 || value > 1.0) throw new ArgumentOutOfRangeException(nameof(DuplicateSimilarityThreshold), "DuplicateSimilarityThreshold must be in [0, 1].");
                _DuplicateSimilarityThreshold = value;
            }
        }

        /// <summary>
        /// Characters of each candidate sent to a reranker. Minimum 100, default 1200.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 100.</exception>
        public int RerankPassageChars
        {
            get
            {
                return _RerankPassageChars;
            }
            set
            {
                if (value < 100) throw new ArgumentOutOfRangeException(nameof(RerankPassageChars), "RerankPassageChars must be at least 100.");
                _RerankPassageChars = Math.Min(value, 20000);
            }
        }

        /// <summary>
        /// Whether searches and chat split a multi-part question into sub-queries before retrieval, when neither the
        /// request nor the scope says. Default false.
        /// </summary>
        public bool QueryDecomposition { get; set; } = false;

        /// <summary>
        /// Whether chat rewrites a question sent with earlier messages into a standalone query before retrieval.
        /// Default true.
        /// </summary>
        public bool ChatConversationRewrite { get; set; } = true;

        /// <summary>
        /// When searches and chat expand the query with a drafted answer and keywords, when neither the request nor the
        /// scope says: Off, On, or Auto (expand searches that are not reranked). Default Auto.
        /// </summary>
        public QueryExpansionModeEnum QueryExpansion
        {
            get
            {
                return _QueryExpansion;
            }
            set
            {
                _QueryExpansion = InputGuard.Defined(value, QueryExpansionModeEnum.Auto);
            }
        }

        /// <summary>
        /// Whether hybrid searches on RecallDB use its single-call hybrid search when the server reports the capability
        /// (both legs, fusion, recency, and collapse on the server). False always runs the two legs separately and
        /// fuses them in NotDory. Default true.
        /// </summary>
        public bool ServerSideHybrid { get; set; } = true;

        /// <summary>
        /// Fusion weight of a search's additional queries (for example decomposed parts) relative to the main query's
        /// 1.0. 0.0 to 1.0, default 1.0.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside [0, 1].</exception>
        public double AdditionalQueryWeight
        {
            get
            {
                return _AdditionalQueryWeight;
            }
            set
            {
                if (!double.IsFinite(value) || value < 0.0 || value > 1.0) throw new ArgumentOutOfRangeException(nameof(AdditionalQueryWeight), "AdditionalQueryWeight must be between 0 and 1.");
                _AdditionalQueryWeight = value;
            }
        }

        /// <summary>
        /// Reciprocal-rank-fusion constant used to fuse the rankings of several queries (extra, decomposed, expanded,
        /// or rewritten). Smaller values keep the main query's ranking more intact against lower-weighted queries.
        /// Minimum 1, maximum 1000, default 5.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside [1, 1000].</exception>
        public int QueryFusionRrfK
        {
            get
            {
                return _QueryFusionRrfK;
            }
            set
            {
                if (value < 1 || value > 1000) throw new ArgumentOutOfRangeException(nameof(QueryFusionRrfK), "QueryFusionRrfK must be between 1 and 1000.");
                _QueryFusionRrfK = value;
            }
        }

        /// <summary>
        /// Fusion weight of query-expansion forms relative to the main query's 1.0. 0.0 to 1.0, default 0.5.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside [0, 1].</exception>
        public double ExpansionWeight
        {
            get
            {
                return _ExpansionWeight;
            }
            set
            {
                if (!double.IsFinite(value) || value < 0.0 || value > 1.0) throw new ArgumentOutOfRangeException(nameof(ExpansionWeight), "ExpansionWeight must be between 0 and 1.");
                _ExpansionWeight = value;
            }
        }

        /// <summary>
        /// How many of the most recent earlier messages chat uses to understand a follow-up. Minimum 1, maximum 20,
        /// default 6 (three exchanges).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside [1, 20].</exception>
        public int ChatHistoryTurns
        {
            get
            {
                return _ChatHistoryTurns;
            }
            set
            {
                if (value < 1 || value > 20) throw new ArgumentOutOfRangeException(nameof(ChatHistoryTurns), "ChatHistoryTurns must be between 1 and 20.");
                _ChatHistoryTurns = value;
            }
        }

        /// <summary>
        /// How many chunks of one memory are embedded at the same time. Minimum 1, maximum 32, default 4. Lower it for an
        /// embedding endpoint that accepts only a few concurrent requests.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside [1, 32].</exception>
        public int EmbeddingParallelism
        {
            get
            {
                return _EmbeddingParallelism;
            }
            set
            {
                if (value < 1 || value > 32) throw new ArgumentOutOfRangeException(nameof(EmbeddingParallelism), "EmbeddingParallelism must be between 1 and 32.");
                _EmbeddingParallelism = value;
            }
        }

        /// <summary>
        /// How many linked memories chat adds to its grounding context by following links from the retrieved ones.
        /// Minimum 0, maximum 10, default 2.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside [0, 10].</exception>
        public int ChatLinkExpansion
        {
            get
            {
                return _ChatLinkExpansion;
            }
            set
            {
                if (value < 0 || value > 10) throw new ArgumentOutOfRangeException(nameof(ChatLinkExpansion), "ChatLinkExpansion must be between 0 and 10.");
                _ChatLinkExpansion = value;
            }
        }

        #endregion

        #region Private-Members

        private QueryExpansionModeEnum _QueryExpansion = QueryExpansionModeEnum.Auto;
        private double _DuplicateSimilarityThreshold = 0.85;
        private int _RerankPassageChars = 1200;
        private int _ChatLinkExpansion = 2;
        private int _ChatHistoryTurns = 6;
        private double _AdditionalQueryWeight = 1.0;
        private int _QueryFusionRrfK = 5;
        private double _ExpansionWeight = 0.5;
        private int _EmbeddingParallelism = 4;

        #endregion
    }
}
