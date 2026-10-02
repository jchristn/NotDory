namespace NotDory.Server.Models
{
    using System;

    /// <summary>
    /// A memory as session start lists it: enough to recognize it and read it in full with memory_read.
    /// </summary>
    public class SessionMemorySummary
    {
        #region Public-Members

        /// <summary>
        /// Memory id.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Slug, unique within its category.
        /// </summary>
        public string Slug { get; set; } = string.Empty;

        /// <summary>
        /// Category name.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Title.
        /// </summary>
        public string? Title { get; set; } = null;

        /// <summary>
        /// One-line summary.
        /// </summary>
        public string? Summary { get; set; } = null;

        /// <summary>
        /// When the memory was last written.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        #endregion
    }
}
