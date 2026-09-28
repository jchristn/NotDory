namespace Isis.Core.Recall
{
    using System;

    /// <summary>
    /// A chat model answered a rerank prompt with scores Isis cannot use (no JSON, or the wrong number of scores). It
    /// concerns one reply, not the endpoint's availability: the search falls back to retrieval order for that query and
    /// the endpoint keeps reranking later searches.
    /// </summary>
    public class RerankReplyException : InvalidOperationException
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">What was wrong with the reply.</param>
        public RerankReplyException(string message) : base(message)
        {
        }

        #endregion
    }
}
