namespace Isis.Server.Models
{
    using Isis.Core.Helpers;

    /// <summary>
    /// What an agent sends when it starts working: the project it is working on, so Isis can hand back that project's
    /// memory scope and context in one call.
    /// </summary>
    public class SessionStartRequest
    {
        #region Public-Members

        /// <summary>
        /// The project or repository name. Matched to a scope by name (case and punctuation ignored). Null picks the
        /// tenant's only scope when there is exactly one.
        /// </summary>
        public string? Project
        {
            get
            {
                return _Project;
            }
            set
            {
                _Project = string.IsNullOrWhiteSpace(value) ? null : InputGuard.MaxLength(value.Trim(), 128, nameof(Project));
            }
        }

        /// <summary>
        /// Create the project's scope when none matches (default true), so a new project has memory from its first session.
        /// </summary>
        public bool CreateIfMissing { get; set; } = true;

        /// <summary>
        /// How many of the most recently written memories to include, 0 to 100 (default 15).
        /// </summary>
        public int MaxMemories
        {
            get
            {
                return _MaxMemories;
            }
            set
            {
                _MaxMemories = InputGuard.Clamp(value, 0, 100);
            }
        }

        #endregion

        #region Private-Members

        private string? _Project = null;
        private int _MaxMemories = 15;

        #endregion
    }
}
