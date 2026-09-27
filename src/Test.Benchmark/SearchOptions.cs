namespace Test.Benchmark
{
    /// <summary>
    /// Optional search settings sent with a benchmark query. Null members are left out so the server default applies.
    /// </summary>
    public class SearchOptions
    {
        #region Public-Members

        /// <summary>
        /// Hybrid recency weight.
        /// </summary>
        public double? RecencyWeight { get; set; } = null;

        /// <summary>
        /// Minimum score.
        /// </summary>
        public double? MinScore { get; set; } = null;

        /// <summary>
        /// Superseded handling: Demote, Hide, or Include.
        /// </summary>
        public string? Superseded { get; set; } = null;

        /// <summary>
        /// Linked memories to add.
        /// </summary>
        public int? LinkExpansion { get; set; } = null;

        /// <summary>
        /// Result diversity (0 to 1).
        /// </summary>
        public double? Diversity { get; set; } = null;

        /// <summary>
        /// Whether to rerank.
        /// </summary>
        public bool? Rerank { get; set; } = null;

        /// <summary>
        /// Minimum rerank score.
        /// </summary>
        public double? MinRerankScore { get; set; } = null;

        /// <summary>
        /// Hybrid text weight (null uses the server's model profile).
        /// </summary>
        public double? TextWeight { get; set; } = null;

        /// <summary>
        /// Hybrid reciprocal-rank-fusion constant (null uses the server's model profile).
        /// </summary>
        public int? RrfK { get; set; } = null;

        /// <summary>
        /// Ask the server to split multi-part questions into sub-queries.
        /// </summary>
        public bool? Decompose { get; set; } = null;

        /// <summary>
        /// Expand each query with a drafted answer and keywords (needs an inference endpoint).
        /// </summary>
        public bool? Expand { get; set; } = null;

        /// <summary>
        /// Fusion weight of the expansion forms; null uses the server's setting.
        /// </summary>
        public double? ExpansionWeight { get; set; } = null;

        /// <summary>
        /// Fusion weight of additional (for example decomposed) queries; null uses the server's setting.
        /// </summary>
        public double? AdditionalQueryWeight { get; set; } = null;

        /// <summary>
        /// Inference endpoint the server uses for decomposition.
        /// </summary>
        public string? InferenceEndpointId { get; set; } = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build options from command-line arguments (--recency-weight, --min-score, --superseded, --link-expansion,
        /// --diversity, --min-rerank-score).
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>The options.</returns>
        public static SearchOptions FromArguments(BenchmarkArguments args)
        {
            SearchOptions options = new SearchOptions();
            if (args.GetOptional("recency-weight") != null) options.RecencyWeight = args.GetDouble("recency-weight", 0.1);
            if (args.GetOptional("min-score") != null) options.MinScore = args.GetDouble("min-score", 0.0);
            options.Superseded = args.GetOptional("superseded");
            if (args.GetOptional("link-expansion") != null) options.LinkExpansion = args.GetInt("link-expansion", 0);
            if (args.GetOptional("diversity") != null) options.Diversity = args.GetDouble("diversity", 0.0);
            if (args.GetOptional("min-rerank-score") != null) options.MinRerankScore = args.GetDouble("min-rerank-score", 0.0);
            if (args.GetOptional("text-weight") != null) options.TextWeight = args.GetDouble("text-weight", 0.5);
            if (args.GetOptional("rrf-k") != null) options.RrfK = args.GetInt("rrf-k", 60);
            // Unset follows the scope's settings (then the server's); --expand/--no-expand and --decompose/--no-decompose
            // force the step on or off for the run.
            if (args.GetFlag("decompose")) options.Decompose = true;
            if (args.GetFlag("no-decompose")) options.Decompose = false;
            if (args.GetFlag("expand")) options.Expand = true;
            if (args.GetFlag("no-expand")) options.Expand = false;
            if (args.GetOptional("expansion-weight") != null) options.ExpansionWeight = args.GetDouble("expansion-weight", 0.5);
            if (args.GetOptional("additional-query-weight") != null) options.AdditionalQueryWeight = args.GetDouble("additional-query-weight", 1.0);
            return options;
        }

        #endregion
    }
}
