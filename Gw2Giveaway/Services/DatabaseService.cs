using Microsoft.Data.Sqlite;

namespace Gw2Giveaway.Services
{
    public class DatabaseService
    {
        private const string ConnectionString = "Data Source=viewers.db";

        private async Task EnsureDatabaseCreatedAsync()
        {
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Viewers (
                    Username TEXT PRIMARY KEY NOT NULL,
                    IqPoints INTEGER NOT NULL DEFAULT 0
                );";
            await command.ExecuteNonQueryAsync();

            bool hasGold = false;
            bool hasQrPoints = false;
            bool hasIqPoints = false;

            var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA table_info(Viewers);";

            using var reader = await pragma.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string columnName = reader.GetString(1);
                if (string.Equals(columnName, "Gold", StringComparison.OrdinalIgnoreCase))
                    hasGold = true;
                if (string.Equals(columnName, "QrPoints", StringComparison.OrdinalIgnoreCase))
                    hasQrPoints = true;
                if (string.Equals(columnName, "IqPoints", StringComparison.OrdinalIgnoreCase))
                    hasIqPoints = true;
            }

            if (!hasIqPoints)
            {
                var addIqColumn = connection.CreateCommand();
                addIqColumn.CommandText = "ALTER TABLE Viewers ADD COLUMN IqPoints INTEGER NOT NULL DEFAULT 0;";
                await addIqColumn.ExecuteNonQueryAsync();
                hasIqPoints = true;
            }

            if (hasQrPoints && hasIqPoints)
            {
                var migrateQr = connection.CreateCommand();
                migrateQr.CommandText = "UPDATE Viewers SET IqPoints = QrPoints WHERE IqPoints = 0;";
                await migrateQr.ExecuteNonQueryAsync();
            }

            if (hasGold && hasIqPoints)
            {
                var migrateGold = connection.CreateCommand();
                migrateGold.CommandText = "UPDATE Viewers SET IqPoints = Gold WHERE IqPoints = 0;";
                await migrateGold.ExecuteNonQueryAsync();
            }
        }

        public async Task AddIqAsync(string username, long amount)
        {
            if (amount == 0) return;

            username = username.ToLowerInvariant();

            await EnsureDatabaseCreatedAsync();

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Viewers (Username, IqPoints)
                VALUES (@username, @amount)
                ON CONFLICT(Username) DO UPDATE SET IqPoints = IqPoints + @amount;";

            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@amount", amount);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<long> GetIqAsync(string username)
        {
            username = username.ToLowerInvariant();

            await EnsureDatabaseCreatedAsync();

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT IqPoints FROM Viewers WHERE Username = @username";
            command.Parameters.AddWithValue("@username", username);

            var result = await command.ExecuteScalarAsync();
            return result == null ? 0L : (long)result;
        }

        public async Task<List<(string Username, long Iq)>> GetLeaderboardAsync(int count = 5)
        {
            await EnsureDatabaseCreatedAsync();

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Username, IqPoints FROM Viewers ORDER BY IqPoints DESC LIMIT @count";
            command.Parameters.AddWithValue("@count", count);

            var list = new List<(string Username, long Iq)>();

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((reader.GetString(0), reader.GetInt64(1)));
            }

            return list;
        }

        public async Task<List<(string Username, long Iq)>> SearchViewersAsync(string? usernameQuery, int limit = 200)
        {
            await EnsureDatabaseCreatedAsync();

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            string query = usernameQuery?.Trim() ?? string.Empty;

            var command = connection.CreateCommand();
            if (string.IsNullOrWhiteSpace(query))
            {
                command.CommandText = "SELECT Username, IqPoints FROM Viewers ORDER BY IqPoints DESC, Username ASC LIMIT @limit";
                command.Parameters.AddWithValue("@limit", limit);
            }
            else
            {
                command.CommandText = "SELECT Username, IqPoints FROM Viewers WHERE Username LIKE @query ORDER BY IqPoints DESC, Username ASC LIMIT @limit";
                command.Parameters.AddWithValue("@query", $"%{query.ToLowerInvariant()}%");
                command.Parameters.AddWithValue("@limit", limit);
            }

            var list = new List<(string Username, long Iq)>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((reader.GetString(0), reader.GetInt64(1)));
            }

            return list;
        }

        public async Task<bool> ClearAllViewersAsync()
        {
            await EnsureDatabaseCreatedAsync();

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Viewers";
            int affected = await command.ExecuteNonQueryAsync();
            return affected >= 0;
        }
    }
}
