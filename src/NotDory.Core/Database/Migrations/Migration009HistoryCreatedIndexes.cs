namespace NotDory.Core.Database.Migrations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NotDory.Core.Database.Mysql;
    using NotDory.Core.Database.SqlServer;

    /// <summary>
    /// Indexes request_history and operation_events by creation time alone. The existing indexes lead with the tenant,
    /// so a global admin listing (newest first, every tenant) and the retention sweep (delete older than a cutoff)
    /// scanned and sorted the whole table, which is the largest in the database once bodies are captured. No-op when
    /// the indexes already exist.
    /// </summary>
    internal sealed class Migration009HistoryCreatedIndexes : ISchemaMigration
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "2026-09-27-history-created-indexes";

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task ApplyAsync(DatabaseDriverBase driver, Func<CancellationToken, Task> ensureSchema, CancellationToken token)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));

            await CreateIndexAsync(driver, "idx_reqhistory_created", "request_history", token).ConfigureAwait(false);
            await CreateIndexAsync(driver, "idx_opevents_created", "operation_events", token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static async Task CreateIndexAsync(DatabaseDriverBase driver, string index, string table, CancellationToken token)
        {
            if (driver is SqlServerDatabaseDriver)
            {
                await driver.ExecuteQueryAsync("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '" + index + "') CREATE INDEX " + index + " ON " + table + " (createdutc);", true, token).ConfigureAwait(false);
                return;
            }

            if (driver is MysqlDatabaseDriver)
            {
                // MySQL has no CREATE INDEX IF NOT EXISTS; a fresh database already has the index from the base schema.
                try
                {
                    await driver.ExecuteQueryAsync("CREATE INDEX " + index + " ON " + table + " (createdutc);", true, token).ConfigureAwait(false);
                }
                catch (Exception e) when (e.Message.IndexOf("Duplicate key name", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                }

                return;
            }

            await driver.ExecuteQueryAsync("CREATE INDEX IF NOT EXISTS " + index + " ON " + table + " (createdutc);", true, token).ConfigureAwait(false);
        }

        #endregion
    }
}
