namespace NotDory.Server.Models
{
    using System.Collections.Generic;
    using NotDory.Core.Models;

    /// <summary>
    /// Everything an agent needs to start using memory, in one response: who it is, its scope, how to use it, and what is
    /// already remembered.
    /// </summary>
    public class SessionStartResult
    {
        #region Public-Members

        /// <summary>
        /// The caller's tenant.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// The caller's principal name (credential name or user email).
        /// </summary>
        public string? Principal { get; set; } = null;

        /// <summary>
        /// The session's scope, or null when none could be chosen (see <see cref="Notice"/> and <see cref="Scopes"/>).
        /// </summary>
        public SessionScopeSummary? Scope { get; set; } = null;

        /// <summary>
        /// How to use NotDory in this session; follow it as standing procedure.
        /// </summary>
        public string Protocol { get; set; } = string.Empty;

        /// <summary>
        /// The scope's categories, with their usage instructions.
        /// </summary>
        public List<Category> Categories { get; set; } = new List<Category>();

        /// <summary>
        /// The scope's effective instructions (tenant-global merged with the scope's own).
        /// </summary>
        public List<ResolvedInstruction> Instructions { get; set; } = new List<ResolvedInstruction>();

        /// <summary>
        /// How many memories the scope holds.
        /// </summary>
        public long MemoryCount { get; set; } = 0;

        /// <summary>
        /// The most recently written memories, newest first.
        /// </summary>
        public List<SessionMemorySummary> RecentMemories { get; set; } = new List<SessionMemorySummary>();

        /// <summary>
        /// The tenant's scopes, listed when the session's scope could not be chosen so the agent can pick one.
        /// </summary>
        public List<SessionScopeSummary>? Scopes { get; set; } = null;

        /// <summary>
        /// Why the scope was not chosen or created, when it was not.
        /// </summary>
        public string? Notice { get; set; } = null;

        #endregion
    }
}
