namespace NotDory.Server.Routes
{
    using System;

    /// <summary>
    /// Thrown when a request body is larger than the server accepts (<c>rest.maxRequestBodyBytes</c>). Reported as 413.
    /// </summary>
    public class RequestTooLargeException : Exception
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">The message returned to the caller.</param>
        public RequestTooLargeException(string message) : base(message)
        {
        }

        #endregion
    }
}
