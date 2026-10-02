namespace NotDory.Core.Recall
{
    using NotDory.Core.Helpers;

    /// <summary>
    /// What NotDory knows about an embedding model family, matched by a fragment of the model name. Every setting is
    /// optional: a null value means the generic default applies. Keep entries to settings a benchmark has shown to
    /// matter, so the table stays small as models change.
    /// </summary>
    public class EmbeddingModelProfile
    {
        #region Public-Members

        /// <summary>
        /// Case-insensitive fragment of the model name this profile applies to, for example "nomic-embed".
        /// </summary>
        public string Match
        {
            get
            {
                return _Match;
            }
            set
            {
                _Match = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Task prefix the model expects in front of stored content. Empty for none.
        /// </summary>
        public string DocumentPrefix
        {
            get
            {
                return _DocumentPrefix;
            }
            set
            {
                _DocumentPrefix = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Task prefix the model expects in front of search queries. Empty for none.
        /// </summary>
        public string QueryPrefix
        {
            get
            {
                return _QueryPrefix;
            }
            set
            {
                _QueryPrefix = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Hybrid text-leg weight (0 to 1) for this model, or null for the generic default.
        /// </summary>
        public double? TextWeight
        {
            get
            {
                return _TextWeight;
            }
            set
            {
                _TextWeight = InputGuard.Clamp(value, 0.0, 1.0);
            }
        }

        /// <summary>
        /// Reciprocal-rank-fusion constant for this model, or null for the generic default.
        /// </summary>
        public int? RrfK
        {
            get
            {
                return _RrfK;
            }
            set
            {
                _RrfK = value.HasValue ? InputGuard.Clamp(value.Value, 1, 1000) : null;
            }
        }

        /// <summary>
        /// Default chunk size as a fraction of the model's token budget, or null for the generic default.
        /// </summary>
        public double? ChunkFraction
        {
            get
            {
                return _ChunkFraction;
            }
            set
            {
                _ChunkFraction = InputGuard.Clamp(value, 0.1, 1.0);
            }
        }

        /// <summary>
        /// Largest default chunk in tokens, or null for the generic default.
        /// </summary>
        public int? ChunkMaxTokens
        {
            get
            {
                return _ChunkMaxTokens;
            }
            set
            {
                _ChunkMaxTokens = value.HasValue ? InputGuard.Clamp(value.Value, 16, 8192) : null;
            }
        }

        #endregion

        #region Private-Members

        private int? _ChunkMaxTokens = null;
        private double? _ChunkFraction = null;
        private int? _RrfK = null;
        private double? _TextWeight = null;
        private string _QueryPrefix = string.Empty;
        private string _DocumentPrefix = string.Empty;
        private string _Match = string.Empty;

        #endregion
    }
}
