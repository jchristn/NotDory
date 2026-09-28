namespace Isis.Server.Models
{
    /// <summary>
    /// A scope as session start lists it.
    /// </summary>
    public class SessionScopeSummary
    {
        #region Public-Members

        /// <summary>
        /// Scope id; pass it to the memory tools.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Scope name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// What the scope holds.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Backing store (RecallDb or Filesystem).
        /// </summary>
        public string StoreProvider { get; set; } = string.Empty;

        /// <summary>
        /// Whether this session start created the scope.
        /// </summary>
        public bool Created { get; set; } = false;

        #endregion
    }
}
