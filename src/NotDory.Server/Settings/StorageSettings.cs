namespace NotDory.Server.Settings
{
    using System;

    /// <summary>
    /// Settings for the Open Knowledge Format (OKF) filesystem mirror that new RecallDb scopes get by default.
    /// </summary>
    public class StorageSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether a new RecallDb scope mirrors its memories to an OKF bundle (in <c>.okf</c> under its targetPath) when the
        /// create request does not say. The mirror needs a targetPath: one on the request, or, for a scope session start
        /// creates, the project path the agent harness sends. A request with <c>filesystemMirror: false</c> still opts out.
        /// Default true.
        /// </summary>
        public bool MirrorByDefault { get; set; } = true;

        #endregion
    }
}
