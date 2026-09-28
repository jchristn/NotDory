namespace Isis.Server.Settings
{
    using System;
    using System.Collections.Generic;
    using Isis.Core.Helpers;

    /// <summary>
    /// What Isis tells an agent when it connects, editable by an administrator: the MCP server instructions (which agent
    /// harnesses place in the model's system prompt, and which session start repeats) and per-tool description overrides.
    /// Unset values use the built-in text (<see cref="AgentProtocol"/>, <see cref="AgentToolCatalog"/>).
    /// </summary>
    public class AgentSettings
    {
        #region Public-Members

        /// <summary>
        /// Maximum length of the server instructions.
        /// </summary>
        public const int MaxInstructionsLength = 16000;

        /// <summary>
        /// Maximum length of one tool description.
        /// </summary>
        public const int MaxToolDescriptionLength = 8000;

        /// <summary>
        /// The server instructions, or null for the built-in protocol.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when longer than <see cref="MaxInstructionsLength"/>.</exception>
        public string? ServerInstructions
        {
            get
            {
                return _ServerInstructions;
            }
            set
            {
                _ServerInstructions = string.IsNullOrWhiteSpace(value) ? null : InputGuard.MaxLength(value.Trim(), MaxInstructionsLength, nameof(ServerInstructions));
            }
        }

        /// <summary>
        /// Tool description overrides by tool name. Names that are not Isis tools and blank descriptions are dropped.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a description is longer than <see cref="MaxToolDescriptionLength"/>.</exception>
        public Dictionary<string, string> ToolDescriptions
        {
            get
            {
                return _ToolDescriptions;
            }
            set
            {
                Dictionary<string, string> cleaned = new Dictionary<string, string>(StringComparer.Ordinal);
                if (value != null)
                {
                    foreach (KeyValuePair<string, string> entry in value)
                    {
                        if (string.IsNullOrEmpty(entry.Key) || AgentToolCatalog.DefaultDescription(entry.Key) == null || string.IsNullOrWhiteSpace(entry.Value)) continue;
                        cleaned[entry.Key] = InputGuard.MaxLength(entry.Value.Trim(), MaxToolDescriptionLength, "toolDescriptions." + entry.Key)!;
                    }
                }

                _ToolDescriptions = cleaned;
            }
        }

        #endregion

        #region Private-Members

        private string? _ServerInstructions = null;
        private Dictionary<string, string> _ToolDescriptions = new Dictionary<string, string>(StringComparer.Ordinal);

        #endregion

        #region Public-Methods

        /// <summary>
        /// The server instructions in effect: the override, or the built-in protocol.
        /// </summary>
        /// <returns>The instructions.</returns>
        public string EffectiveServerInstructions()
        {
            return _ServerInstructions ?? AgentProtocol.ServerInstructions;
        }

        /// <summary>
        /// The description in effect for a tool: the override, or the default.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <returns>The description, or null for an unknown tool.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
        public string? EffectiveToolDescription(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            return _ToolDescriptions.TryGetValue(name, out string? description) ? description : AgentToolCatalog.DefaultDescription(name);
        }

        #endregion
    }
}
