namespace NotDory.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using NotDory.Core.Enums;
    using NotDory.Core.Models;
    using NotDory.Core.Recall;
    using NotDory.Core.Stores;

    /// <summary>
    /// Expands a query with two model-drafted forms that target the two legs of hybrid search: a short hypothetical
    /// answer, searched by vector because it reads like a stored memory, and keywords a relevant memory would contain,
    /// searched as text. Both are fused at a lower weight than the original query, so the original keeps the rankings it
    /// already has right. The prompt is model-agnostic; a reply that cannot be read adds nothing.
    /// </summary>
    public class QueryExpander
    {
        #region Public-Members

        /// <summary>
        /// Most keywords kept. Minimum 1, maximum 20, default 8.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside [1, 20].</exception>
        public int MaxKeywords
        {
            get
            {
                return _MaxKeywords;
            }
            set
            {
                if (value < 1 || value > 20) throw new ArgumentOutOfRangeException(nameof(MaxKeywords), "MaxKeywords must be between 1 and 20.");
                _MaxKeywords = value;
            }
        }

        /// <summary>
        /// Longest hypothetical answer kept, in characters; longer answers keep their start. Minimum 100, maximum 4000,
        /// default 600.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside [100, 4000].</exception>
        public int MaxAnswerChars
        {
            get
            {
                return _MaxAnswerChars;
            }
            set
            {
                if (value < 100 || value > 4000) throw new ArgumentOutOfRangeException(nameof(MaxAnswerChars), "MaxAnswerChars must be between 100 and 4000.");
                _MaxAnswerChars = value;
            }
        }

        /// <summary>
        /// Longest wait for the model before searching the original query alone. Minimum 1 second, default 20.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1 second.</exception>
        public TimeSpan Timeout
        {
            get
            {
                return _Timeout;
            }
            set
            {
                if (value < TimeSpan.FromSeconds(1) || value > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(Timeout), "Timeout must be between 1 second and 5 minutes.");
                _Timeout = value;
            }
        }

        #endregion

        #region Private-Members

        private readonly InferenceService _InferenceService;
        private int _MaxKeywords = 8;
        private int _MaxAnswerChars = 600;
        private TimeSpan _Timeout = TimeSpan.FromSeconds(20);

        private const string _SystemPrompt = "You help a retrieval system find stored notes. You write search material; you do not chat.";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the expander.
        /// </summary>
        /// <param name="inferenceService">The inference service used to call the model.</param>
        /// <exception cref="ArgumentNullException">Thrown when inferenceService is null.</exception>
        public QueryExpander(InferenceService inferenceService)
        {
            _InferenceService = inferenceService ?? throw new ArgumentNullException(nameof(inferenceService));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Draft the expansion forms of a query.
        /// </summary>
        /// <param name="endpoint">The inference endpoint to ask.</param>
        /// <param name="question">The query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The expansion; empty when the reply could not be used or the model failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when endpoint or question is null.</exception>
        public async Task<QueryExpansion> ExpandAsync(ModelEndpoint endpoint, string question, CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (question == null) throw new ArgumentNullException(nameof(question));

            string trimmed = question.Trim();
            string prompt =
                "A user is searching a store of short notes with the question below. Write two things.\n" +
                "1. answer: one to three sentences written as the note that would answer the question, in the plain, factual " +
                "style of a stored note. If you do not know the facts, write a plausible note anyway; it is only used to find " +
                "similar notes.\n" +
                "2. keywords: up to " + _MaxKeywords + " words or short phrases a note answering the question would likely contain, " +
                "including synonyms and any names or identifiers from the question exactly as written.\n" +
                "Reply with JSON only, in the form {\"answer\": \"...\", \"keywords\": [\"...\"]}.\n\nQuestion: " + trimmed;

            try
            {
                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(_Timeout);
                string reply = await _InferenceService.CompleteAsync(endpoint, _SystemPrompt, prompt, cts.Token).ConfigureAwait(false);
                return Parse(reply, trimmed, _MaxKeywords, _MaxAnswerChars);
            }
            catch (Exception e) when (!(e is OperationCanceledException && token.IsCancellationRequested))
            {
                // Expansion is an optimization; any failure searches the original query alone.
                return new QueryExpansion();
            }
        }

        /// <summary>
        /// Read the expansion out of a model reply.
        /// </summary>
        /// <param name="reply">The model's reply.</param>
        /// <param name="question">The original query.</param>
        /// <param name="maxKeywords">Most keywords to keep.</param>
        /// <param name="maxAnswerChars">Longest hypothetical answer to keep.</param>
        /// <returns>The expansion; empty when the reply holds no usable JSON.</returns>
        public static QueryExpansion Parse(string? reply, string question, int maxKeywords, int maxAnswerChars)
        {
            QueryExpansion expansion = new QueryExpansion();
            if (string.IsNullOrWhiteSpace(reply)) return expansion;

            // Reasoning models may prefix their answer with a thinking block.
            string text = Regex.Replace(reply, "<think>.*?</think>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return expansion;

            try
            {
                using JsonDocument document = JsonDocument.Parse(text.Substring(start, end - start + 1));
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return expansion;

                if (root.TryGetProperty("answer", out JsonElement answer) && answer.ValueKind == JsonValueKind.String)
                {
                    string value = Regex.Replace((answer.GetString() ?? string.Empty).Trim(), "\\s+", " ");
                    if (value.Length > maxAnswerChars) value = value.Substring(0, maxAnswerChars);
                    if (!string.Equals(value, question, StringComparison.OrdinalIgnoreCase)) expansion.HypotheticalAnswer = value;
                }

                if (root.TryGetProperty("keywords", out JsonElement keywords) && keywords.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in keywords.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String) continue;
                        string keyword = (item.GetString() ?? string.Empty).Trim();
                        if (keyword.Length == 0 || expansion.Keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase)) continue;
                        expansion.Keywords.Add(keyword);
                        if (expansion.Keywords.Count >= maxKeywords) break;
                    }
                }
            }
            catch (JsonException)
            {
                return new QueryExpansion();
            }

            return expansion;
        }

        /// <summary>
        /// Turn an expansion into weighted sub-queries for a search. In Hybrid mode the hypothetical answer is searched by
        /// vector and the keywords as text; in Semantic mode only the answer is used, and in Keyword mode only the
        /// keywords.
        /// </summary>
        /// <param name="expansion">The expansion.</param>
        /// <param name="mode">The search's mode.</param>
        /// <param name="weight">Each sub-query's fusion weight, 0.0 to 1.0.</param>
        /// <returns>The sub-queries; empty when the expansion is empty or the weight is 0.</returns>
        public static List<MemorySubQuery> ToSubQueries(QueryExpansion expansion, SearchModeEnum mode, double weight)
        {
            List<MemorySubQuery> result = new List<MemorySubQuery>();
            if (expansion == null || expansion.IsEmpty || weight <= 0.0) return result;

            if (mode != SearchModeEnum.Keyword && !string.IsNullOrWhiteSpace(expansion.HypotheticalAnswer))
                result.Add(new MemorySubQuery { Text = expansion.HypotheticalAnswer, Weight = weight, Mode = SearchModeEnum.Semantic });
            if (mode != SearchModeEnum.Semantic && expansion.Keywords.Count > 0)
                result.Add(new MemorySubQuery { Text = string.Join(" ", expansion.Keywords), Weight = weight, Mode = SearchModeEnum.Keyword });
            return result;
        }

        #endregion
    }
}
