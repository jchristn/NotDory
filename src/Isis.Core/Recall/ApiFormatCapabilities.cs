namespace Isis.Core.Recall
{
    using Isis.Core.Enums;

    /// <summary>
    /// What an inference endpoint can do, decided by its API format. Every inference endpoint is configured the same
    /// way; the format decides which jobs a scope may give it: chat models (Ollama, OpenAI, vLLM, Gemini) answer chat,
    /// run query steps, and rerank by rating candidates in a prompt; cross-encoders (TEI and Cohere rerank APIs) only
    /// rerank. Every format is served through PolyPrompt.
    /// </summary>
    public static class ApiFormatCapabilities
    {
        #region Public-Methods

        /// <summary>
        /// Whether an endpoint of this format can generate text (answer chat, rewrite, split, and expand queries).
        /// </summary>
        /// <param name="format">The API format.</param>
        /// <returns>True for chat formats.</returns>
        public static bool CanChat(ApiFormatEnum format)
        {
            return format == ApiFormatEnum.Ollama || format == ApiFormatEnum.OpenAI || format == ApiFormatEnum.VLlm || format == ApiFormatEnum.Gemini;
        }

        /// <summary>
        /// Whether an endpoint of this format can rerank search candidates: cross-encoders through their rerank API, and
        /// chat models by rating the candidates in a prompt.
        /// </summary>
        /// <param name="format">The API format.</param>
        /// <returns>True when the format can rerank.</returns>
        public static bool CanRerank(ApiFormatEnum format)
        {
            return format == ApiFormatEnum.Tei || format == ApiFormatEnum.Cohere || CanChat(format);
        }

        /// <summary>
        /// Whether the format is a cross-encoder rerank API that cannot generate text.
        /// </summary>
        /// <param name="format">The API format.</param>
        /// <returns>True for TEI and Cohere-compatible rerank APIs.</returns>
        public static bool IsRerankOnly(ApiFormatEnum format)
        {
            return format == ApiFormatEnum.Tei || format == ApiFormatEnum.Cohere;
        }

        #endregion
    }
}
