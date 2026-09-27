namespace Isis.Server.Services
{
    using System.Collections.Generic;

    /// <summary>
    /// The model-drafted forms of a query used for expansion: a short hypothetical answer and a list of keywords.
    /// </summary>
    public class QueryExpansion
    {
        #region Public-Members

        /// <summary>
        /// A short passage written as if it answered the question, searched by vector. Empty when the model gave none.
        /// </summary>
        public string HypotheticalAnswer { get; set; } = string.Empty;

        /// <summary>
        /// Terms a relevant memory would likely contain, searched as text. Empty when the model gave none.
        /// </summary>
        public List<string> Keywords { get; set; } = new List<string>();

        /// <summary>
        /// True when neither form is present.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrWhiteSpace(HypotheticalAnswer) && Keywords.Count == 0;
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an empty expansion.
        /// </summary>
        public QueryExpansion()
        {
        }

        #endregion
    }
}
