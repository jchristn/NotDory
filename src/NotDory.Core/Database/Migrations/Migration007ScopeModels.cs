namespace NotDory.Core.Database.Migrations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NotDory.Core.Database.Mysql;
    using NotDory.Core.Database.SqlServer;

    /// <summary>
    /// Adds the scope model and query settings (inferenceendpointid, queryendpointid, conversationrewrite,
    /// queryexpansion, querydecomposition). Uses ALTER TABLE; every existing scope takes null for each, which means the
    /// server defaults. No-op on a database already in the new shape.
    /// </summary>
    internal sealed class Migration007ScopeModels : ISchemaMigration
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "2026-09-27-scope-models";

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task ApplyAsync(DatabaseDriverBase driver, Func<CancellationToken, Task> ensureSchema, CancellationToken token)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));

            bool sqlServer = driver is SqlServerDatabaseDriver;
            bool mysql = driver is MysqlDatabaseDriver;
            string add = sqlServer ? "ALTER TABLE scopes ADD " : "ALTER TABLE scopes ADD COLUMN ";
            string idType = sqlServer ? "NVARCHAR(64) NULL" : (mysql ? "VARCHAR(64) NULL" : "TEXT");
            string flagType = sqlServer || mysql ? "INT NULL" : "INTEGER";
            string modeType = sqlServer ? "NVARCHAR(16) NULL" : (mysql ? "VARCHAR(16) NULL" : "TEXT");

            string[][] columns = new string[][]
            {
                new string[] { "inferenceendpointid", idType },
                new string[] { "queryendpointid", idType },
                new string[] { "conversationrewrite", flagType },
                new string[] { "queryexpansion", modeType },
                new string[] { "querydecomposition", flagType }
            };

            foreach (string[] column in columns)
            {
                if (await ColumnExistsAsync(driver, column[0], token).ConfigureAwait(false)) continue;
                await driver.ExecuteQueryAsync(add + column[0] + " " + column[1] + ";", true, token).ConfigureAwait(false);
            }
        }

        #endregion

        #region Private-Methods

        private static async Task<bool> ColumnExistsAsync(DatabaseDriverBase driver, string column, CancellationToken token)
        {
            try
            {
                await driver.ExecuteQueryAsync("SELECT " + column + " FROM scopes WHERE 1 = 0;", false, token).ConfigureAwait(false);
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
