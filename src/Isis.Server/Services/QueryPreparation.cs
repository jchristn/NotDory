namespace Isis.Server.Services
{
    /// <summary>
    /// What <see cref="QueryPreparer"/> did to a query before retrieval.
    /// </summary>
    public class QueryPreparation
    {
        #region Public-Members

        /// <summary>
        /// The standalone form of a follow-up question that was searched alongside it, or null.
        /// </summary>
        public string? StandaloneQuestion { get; set; } = null;

        /// <summary>
        /// A notice for the caller, for example when a requested step had no model to run on; otherwise null.
        /// </summary>
        public string? Notice { get; set; } = null;

        /// <summary>
        /// Whether the query was expanded with a drafted answer and keywords.
        /// </summary>
        public bool Expanded { get; set; } = false;

        /// <summary>
        /// Whether the query was split into parts.
        /// </summary>
        public bool Decomposed { get; set; } = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an empty preparation.
        /// </summary>
        public QueryPreparation()
        {
        }

        #endregion
    }
}
