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
                    Gold INTEGER NOT NULL DEFAULT 0
                );";
            await command.ExecuteNonQueryAsync();
        }

        public async Task AddGoldAsync(string username, long amount)
        {
            if (amount == 0) return;

            username = username.ToLowerInvariant();

            await EnsureDatabaseCreatedAsync();

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Viewers (Username, Gold)
                VALUES (@username, @amount)
                ON CONFLICT(Username) DO UPDATE SET Gold = Gold + @amount;";

            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@amount", amount);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<long> GetGoldAsync(string username)
        {
            username = username.ToLowerInvariant();

            await EnsureDatabaseCreatedAsync();

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Gold FROM Viewers WHERE Username = @username";
            command.Parameters.AddWithValue("@username", username);

            var result = await command.ExecuteScalarAsync();
            return result == null ? 0L : (long)result;
        }

        public async Task<List<(string Username, long Gold)>> GetLeaderboardAsync(int count = 5)
        {
            await EnsureDatabaseCreatedAsync();

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Username, Gold FROM Viewers ORDER BY Gold DESC LIMIT @count";
            command.Parameters.AddWithValue("@count", count);

            var list = new List<(string Username, long Gold)>();

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add((reader.GetString(0), reader.GetInt64(1)));
            }

            return list;
        }
    }
}
