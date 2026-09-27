namespace Isis.Server.Routes
{
    using System;

    /// <summary>
    /// The HTTP error an unhandled exception is answered with.
    /// </summary>
    public class ErrorClassification
    {
        #region Public-Members

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Short error name, for example ServiceUnavailable.
        /// </summary>
        public string Error { get; }

        /// <summary>
        /// Message safe to return to the client.
        /// </summary>
        public string Message { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a classification.
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="error">Short error name.</param>
        /// <param name="message">Message safe to return to the client.</param>
        /// <exception cref="ArgumentNullException">Thrown when error or message is null.</exception>
        public ErrorClassification(int statusCode, string error, string message)
        {
            StatusCode = statusCode;
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        #endregion
    }
}
