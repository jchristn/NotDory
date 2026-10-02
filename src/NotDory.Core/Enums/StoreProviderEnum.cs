namespace NotDory.Core.Enums
{
    /// <summary>
    /// The memory store backing a scope. Determines what retrieval capabilities are available.
    /// </summary>
    public enum StoreProviderEnum
    {
        /// <summary>
        /// RecallDB (default). System of record for memory content and embeddings; supports vector,
        /// full-text, and hybrid search. Requires a configured embedding endpoint.
        /// </summary>
        RecallDb,

        /// <summary>
        /// Filesystem. Memory stored as flat files at a target path; keyword/metadata search only.
        /// </summary>
        Filesystem
    }
}
