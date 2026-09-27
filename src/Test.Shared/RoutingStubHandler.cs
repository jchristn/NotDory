namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A fake HTTP server for SDK clients: a callback chooses the status and body for each request from its method,
    /// path, and body, and every request is recorded as "METHOD /path body".
    /// </summary>
    internal sealed class RoutingStubHandler : HttpMessageHandler
    {
        #region Internal-Members

        internal List<string> Requests { get; } = new List<string>();

        #endregion

        #region Private-Members

        private readonly Func<string, string, string, KeyValuePair<HttpStatusCode, string>> _Respond;
        private readonly object _Lock = new object();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the handler.
        /// </summary>
        /// <param name="respond">Given the method, path, and body, returns the status and JSON body to answer with.</param>
        internal RoutingStubHandler(Func<string, string, string, KeyValuePair<HttpStatusCode, string>> respond)
        {
            _Respond = respond ?? throw new ArgumentNullException(nameof(respond));
        }

        #endregion

        #region Protected-Methods

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) : string.Empty;
            string method = request.Method.Method;
            string path = request.RequestUri?.AbsolutePath ?? "/";
            lock (_Lock) Requests.Add(method + " " + path + " " + body);

            KeyValuePair<HttpStatusCode, string> answer = _Respond(method, path, body);
            return new HttpResponseMessage(answer.Key) { Content = new StringContent(answer.Value, Encoding.UTF8, "application/json") };
        }

        #endregion
    }
}
