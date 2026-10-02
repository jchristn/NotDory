namespace NotDory.Core.Database.Migrations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NotDory.Core.Database.Mysql;
    using NotDory.Core.Database.SqlServer;

    /// <summary>
    /// Adds the reasoning setting to model endpoints (how much a reasoning model thinks on NotDory's calls). Every existing
    /// endpoint takes 'Default', which sends no reasoning setting, so behavior is unchanged. No-op when the column exists.
    /// </summary>
    internal sealed class Migration010EndpointReasoning : ISchemaMigration
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "2026-09-27-endpoint-reasoning";

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task ApplyAsync(DatabaseDriverBase driver, Func<CancellationToken, Task> ensureSchema, CancellationToken token)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (await ColumnExistsAsync(driver, token).ConfigureAwait(false)) return;

            string statement;
            if (driver is SqlServerDatabaseDriver) statement = "ALTER TABLE model_endpoints ADD reasoning NVARCHAR(16) NOT NULL DEFAULT 'Default';";
            else if (driver is MysqlDatabaseDriver) statement = "ALTER TABLE model_endpoints ADD COLUMN reasoning VARCHAR(16) NOT NULL DEFAULT 'Default';";
            else statement = "ALTER TABLE model_endpoints ADD COLUMN reasoning TEXT NOT NULL DEFAULT 'Default';";
            await driver.ExecuteQueryAsync(statement, true, token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static async Task<bool> ColumnExistsAsync(DatabaseDriverBase driver, CancellationToken token)
        {
            try
            {
                await driver.ExecuteQueryAsync("SELECT reasoning FROM model_endpoints WHERE 1 = 0;", false, token).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
