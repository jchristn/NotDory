namespace Isis.Core.Enums
{
    /// <summary>
    /// How much a reasoning ("thinking") model thinks on the calls Isis makes to an inference endpoint: chat answers, query
    /// steps, and reranking. Models differ in what they honor (Ollama's gpt-oss takes a level but ignores off; qwen3 turns
    /// off but treats every level as on), so the setting belongs to the endpoint that names the model.
    /// </summary>
    public enum ReasoningModeEnum
    {
        /// <summary>
        /// Send no reasoning setting; the model's own default applies.
        /// </summary>
        Default,

        /// <summary>
        /// Turn thinking off where the provider allows it (Ollama think false, a Gemini budget of 0; OpenAI "minimal").
        /// </summary>
        Off,

        /// <summary>
        /// Low reasoning effort.
        /// </summary>
        Low,

        /// <summary>
        /// Medium reasoning effort.
        /// </summary>
        Medium,

        /// <summary>
        /// High reasoning effort.
        /// </summary>
        High
    }
}
