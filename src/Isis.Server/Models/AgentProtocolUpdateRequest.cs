namespace Isis.Server.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Replaces what agents are told when they connect. Omitted, null, or blank values use the built-in text.
    /// </summary>
    public class AgentProtocolUpdateRequest
    {
        #region Public-Members

        /// <summary>
        /// The MCP server instructions, or null for the built-in protocol.
        /// </summary>
        public string? ServerInstructions { get; set; } = null;

        /// <summary>
        /// Tool description overrides by tool name; a tool left out, or given a blank description, uses its default.
        /// </summary>
        public Dictionary<string, string?>? ToolDescriptions { get; set; } = null;

        #endregion
    }
}
