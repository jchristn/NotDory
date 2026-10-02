namespace NotDory.Core.Database.Migrations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NotDory.Core.Database.Mysql;
    using NotDory.Core.Database.SqlServer;

    /// <summary>
    /// Adds the scope filesystem mirror flag (filesystemmirror). Uses ALTER TABLE; every existing scope takes 0, so no
    /// scope starts mirroring. No-op on a database already in the new shape.
    /// </summary>
    internal sealed class Migration012ScopeFilesystemMirror : ISchemaMigration
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "2026-10-01-scope-filesystem-mirror";

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task ApplyAsync(DatabaseDriverBase driver, Func<CancellationToken, Task> ensureSchema, CancellationToken token)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (await ColumnExistsAsync(driver, "filesystemmirror", token).ConfigureAwait(false)) return;

            bool sqlServer = driver is SqlServerDatabaseDriver;
            bool mysql = driver is MysqlDatabaseDriver;
            string add = sqlServer ? "ALTER TABLE scopes ADD " : "ALTER TABLE scopes ADD COLUMN ";
            string type = sqlServer || mysql ? "INT NOT NULL DEFAULT 0" : "INTEGER NOT NULL DEFAULT 0";
            await driver.ExecuteQueryAsync(add + "filesystemmirror " + type + ";", true, token).ConfigureAwait(false);
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
