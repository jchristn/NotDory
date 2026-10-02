namespace NotDory.Server.Models
{
    using NotDory.Core.Helpers;

    /// <summary>
    /// What an agent sends when it starts working: the project it is working on, so NotDory can hand back that project's
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
        /// The git remote URL of the working copy (for example https://github.com/owner/repo.git or
        /// git@github.com:owner/repo.git). Its repository name is tried after <see cref="Project"/>: it is stable across
        /// clones, whatever the local folder is called. Sent by harness session hooks.
        /// </summary>
        public string? Remote
        {
            get
            {
                return _Remote;
            }
            set
            {
                _Remote = string.IsNullOrWhiteSpace(value) ? null : InputGuard.MaxLength(value.Trim(), 2048, nameof(Remote));
            }
        }

        /// <summary>
        /// The working directory's name, tried last. On its own (no project and no remote) it only finds an existing scope;
        /// it never creates one, so opening an arbitrary folder does not add a scope.
        /// </summary>
        public string? Directory
        {
            get
            {
                return _Directory;
            }
            set
            {
                _Directory = string.IsNullOrWhiteSpace(value) ? null : InputGuard.MaxLength(value.Trim(), 256, nameof(Directory));
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
        private string? _Remote = null;
        private string? _Directory = null;
        private int _MaxMemories = 15;

        #endregion
    }
}
