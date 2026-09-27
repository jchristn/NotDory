namespace Test.Shared
{
    using System.Data.Common;

    /// <summary>
    /// A database exception whose transience a test chooses, standing in for a provider exception such as Npgsql's
    /// pool-exhausted or too-many-connections errors.
    /// </summary>
    public class StubDbException : DbException
    {
        #region Public-Members

        /// <inheritdoc />
        public override bool IsTransient { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="isTransient">Whether retrying may succeed.</param>
        public StubDbException(string message, bool isTransient) : base(message)
        {
            IsTransient = isTransient;
        }

        #endregion
    }
}
