namespace Isis.Core.Enums
{
    /// <summary>
    /// When a scope's searches are expanded with a drafted answer and keywords before retrieval.
    /// </summary>
    public enum QueryExpansionModeEnum
    {
        /// <summary>
        /// Never expand.
        /// </summary>
        Off,

        /// <summary>
        /// Always expand when a query model is available.
        /// </summary>
        On,

        /// <summary>
        /// Expand when a query model is available and the search is not reranked. Expansion helps plain hybrid search on
        /// every benchmark dataset, while a reranker already recovers most of what it adds.
        /// </summary>
        Auto
    }
}
