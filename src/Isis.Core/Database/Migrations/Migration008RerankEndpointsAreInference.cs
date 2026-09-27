namespace Isis.Core.Database.Migrations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Folds the separate Rerank endpoint kind into Inference: a reranker is configured like any other inference
    /// endpoint, and its API format decides what it can do. Rerank endpoints that used the VLlm format called vLLM's
    /// Cohere-compatible rerank API, so they become Cohere (VLlm on an inference endpoint now means chat). Ids and every
    /// scope that points at the endpoints are unchanged. No-op when no Rerank endpoints remain.
    /// </summary>
    internal sealed class Migration008RerankEndpointsAreInference : ISchemaMigration
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "2026-09-27-rerank-endpoints-are-inference";

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task ApplyAsync(DatabaseDriverBase driver, Func<CancellationToken, Task> ensureSchema, CancellationToken token)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));

            await driver.ExecuteQueryAsync("UPDATE model_endpoints SET apiformat = 'Cohere' WHERE kind = 'Rerank' AND apiformat = 'VLlm';", true, token).ConfigureAwait(false);
            await driver.ExecuteQueryAsync("UPDATE model_endpoints SET kind = 'Inference' WHERE kind = 'Rerank';", true, token).ConfigureAwait(false);
        }

        #endregion
    }
}
