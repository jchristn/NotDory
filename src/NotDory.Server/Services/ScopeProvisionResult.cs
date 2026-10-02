namespace NotDory.Server.Services
{
    using System;
    using NotDory.Core.Models;

    /// <summary>
    /// The outcome of creating a scope: the created scope, or the HTTP status and message that explain why not.
    /// </summary>
    public class ScopeProvisionResult
    {
        #region Public-Members

        /// <summary>
        /// The created scope, or null when creation was refused.
        /// </summary>
        public Scope? Scope { get; }

        /// <summary>
        /// HTTP status: 201 on success, 400 or 409 for a refusal.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Short error name for a refusal (BadRequest or Conflict), or null on success.
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// Actionable message for a refusal, or null on success.
        /// </summary>
        public string? Message { get; }

        #endregion

        #region Constructors-and-Factories

        private ScopeProvisionResult(Scope? scope, int statusCode, string? error, string? message)
        {
            Scope = scope;
            StatusCode = statusCode;
            Error = error;
            Message = message;
        }

        /// <summary>
        /// A successful creation.
        /// </summary>
        /// <param name="scope">The created scope.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when scope is null.</exception>
        public static ScopeProvisionResult Created(Scope scope)
        {
            return new ScopeProvisionResult(scope ?? throw new ArgumentNullException(nameof(scope)), 201, null, null);
        }

        /// <summary>
        /// A refused creation.
        /// </summary>
        /// <param name="statusCode">HTTP status (400 or 409).</param>
        /// <param name="error">Short error name.</param>
        /// <param name="message">Actionable message.</param>
        /// <returns>The result.</returns>
        public static ScopeProvisionResult Refused(int statusCode, string error, string message)
        {
            return new ScopeProvisionResult(null, statusCode, error, message);
        }

        #endregion
    }
}
