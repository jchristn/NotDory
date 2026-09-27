namespace Isis.Server.Routes
{
    using System;
    using System.Data.Common;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Isis.Core.Recall;

    /// <summary>
    /// Maps an exception no route handled to the status code routes use for it, so clients can tell a bad request
    /// from a backing service that is briefly unavailable (and worth retrying) from a genuine server fault.
    /// </summary>
    public static class ErrorClassifier
    {
        #region Public-Methods

        /// <summary>
        /// Classify an exception.
        /// </summary>
        /// <param name="e">The exception.</param>
        /// <param name="clientCancelled">Whether the client's request was cancelled (a cancellation is then not a timeout).</param>
        /// <returns>The status code, error name, and client-safe message.</returns>
        /// <exception cref="ArgumentNullException">Thrown when e is null.</exception>
        public static ErrorClassification Classify(Exception e, bool clientCancelled)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            if (e is RequestTooLargeException) return new ErrorClassification(413, "PayloadTooLarge", e.Message);
            if (e is ModelEndpointUnavailableException) return new ErrorClassification(503, "ServiceUnavailable", e.Message);
            if (e is ArgumentException || e is InvalidOperationException || e is FormatException) return new ErrorClassification(400, "BadRequest", e.Message);
            if (e is NotSupportedException || e is NotImplementedException) return new ErrorClassification(501, "NotImplemented", e.Message);

            // The database is overloaded, restarting, or out of connections (for example a shared Postgres at its
            // max_connections, or this server's pool waiting past its timeout). Retrying later can succeed.
            if (e is DbException dbException && dbException.IsTransient)
                return new ErrorClassification(503, "ServiceUnavailable", "The database is temporarily unavailable or overloaded; retry shortly.");

            if (e is HttpRequestException || (e is TaskCanceledException && !clientCancelled) || e is TimeoutException)
                return new ErrorClassification(503, "ServiceUnavailable", "A backing service (memory store, database, or model endpoint) could not be reached or timed out.");

            return new ErrorClassification(500, "InternalError", "An unexpected error occurred. It has been logged.");
        }

        #endregion
    }
}
