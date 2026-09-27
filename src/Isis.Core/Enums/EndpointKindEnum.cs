namespace Isis.Core.Enums
{
    /// <summary>
    /// The kind of AI model endpoint being configured.
    /// </summary>
    public enum EndpointKindEnum
    {
        /// <summary>
        /// An embedding endpoint that turns text into a vector. Used to write and query RecallDB scopes.
        /// </summary>
        Embedding,

        /// <summary>
        /// An inference endpoint: every model that is not an embedding model. A scope gives it jobs (chat answers, query
        /// rewriting and expansion, reranking), and its API format decides which it can do (see
        /// <c>ApiFormatCapabilities</c>): chat models do all of them, cross-encoders (Tei, Cohere) only rerank.
        /// </summary>
        Inference,

        /// <summary>
        /// Legacy value from when rerankers were a separate kind. Accepted on input and stored as
        /// <see cref="Inference"/>; existing rep_ ids stay valid.
        /// </summary>
        Rerank
    }
}
