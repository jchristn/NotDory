namespace Isis.Core.Recall
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using Isis.Core.Enums;
    using Isis.Core.Models;
    using PolyPrompt.Clients;

    /// <summary>
    /// Builds the PolyPrompt client for an inference endpoint, so every job (chat, query steps, reranking) talks to a
    /// model the same way: the endpoint's API format picks the client, and Isis's generic authentication is applied by
    /// a per-endpoint handler over the shared transport (which retries transient failures).
    /// </summary>
    public static class ModelClientFactory
    {
        #region Public-Methods

        /// <summary>
        /// Create a client for an endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint.</param>
        /// <param name="transport">The shared HTTP transport; it is not disposed with the client.</param>
        /// <param name="timeoutMs">The client's request timeout in milliseconds, at least 1.</param>
        /// <returns>The client; the caller disposes it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when endpoint or transport is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when timeoutMs is below 1.</exception>
        public static CompletionClientBase Create(ModelEndpoint endpoint, HttpMessageHandler transport, int timeoutMs)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (timeoutMs < 1) throw new ArgumentOutOfRangeException(nameof(timeoutMs), "The timeout must be at least 1 ms.");

            string baseUrl = endpoint.GetBaseUrl();
            CompletionClientBase client;
            if (endpoint.ApiFormat == ApiFormatEnum.Gemini)
            {
                // Gemini presents its credential via PolyPrompt's native "?key=" query handling, so hand the secret to the
                // client directly rather than through the generic auth handler.
                HttpClient geminiTransport = new HttpClient(transport, false) { Timeout = Timeout.InfiniteTimeSpan };
                client = new GeminiClient(baseUrl, endpoint.AuthSecret ?? string.Empty, null, geminiTransport);
            }
            else
            {
                // All other formats: Isis's generic auth (bearer, header, query, basic, access-secret) through a
                // per-endpoint delegating handler, and a null key so PolyPrompt does not add its own Authorization header.
                HttpClient authed = new HttpClient(new EndpointAuthHandler(endpoint, transport), false) { Timeout = Timeout.InfiniteTimeSpan };
                switch (endpoint.ApiFormat)
                {
                    case ApiFormatEnum.Ollama:
                        client = new OllamaClient(baseUrl, null, null, authed);
                        break;
                    case ApiFormatEnum.Tei:
                        client = new TeiClient(baseUrl, null, null, authed);
                        break;
                    case ApiFormatEnum.Cohere:
                        client = new CohereClient(baseUrl, null, null, authed);
                        break;
                    default:
                        client = new OpenAiClient(baseUrl, null, null, authed);
                        break;
                }
            }

            if (!string.IsNullOrEmpty(endpoint.Model)) client.Model = endpoint.Model;
            client.TimeoutMs = timeoutMs;
            return client;
        }

        #endregion
    }
}
