namespace NotDory.Core.Enums
{
    /// <summary>
    /// The wire format an AI model endpoint speaks, controlling request/response shaping and the
    /// default health-check probe path.
    /// </summary>
    public enum ApiFormatEnum
    {
        /// <summary>
        /// Ollama native API (probe path <c>/api/tags</c>).
        /// </summary>
        Ollama,

        /// <summary>
        /// OpenAI-compatible API (probe path <c>/v1/models</c>).
        /// </summary>
        OpenAI,

        /// <summary>
        /// vLLM OpenAI-compatible server.
        /// </summary>
        VLlm,

        /// <summary>
        /// Google Gemini API (probe path <c>/v1beta/models</c>, auth header <c>x-goog-api-key</c>).
        /// </summary>
        Gemini,

        /// <summary>
        /// Hugging Face Text Embeddings Inference reranker API (<c>POST /rerank</c> with <c>query</c> and <c>texts</c>;
        /// probe path <c>/health</c>). A cross-encoder: rerank only.
        /// </summary>
        Tei,

        /// <summary>
        /// Cohere-compatible rerank API (<c>POST /v1/rerank</c> with <c>query</c> and <c>documents</c>, answering
        /// <c>results[].relevance_score</c>), Cohere's v2 rerank API (<c>/v2/rerank</c>), also served by vLLM. A cross-encoder: rerank
        /// endpoints only.
        /// </summary>
        Cohere
    }
}
