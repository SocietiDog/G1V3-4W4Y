using Microsoft.Data.Sqlite;
using Gw2Giveaway;  // GiveawayHistoryEntry
using System.IO;

namespace Gw2Giveaway.Services
{
    public class DatabaseService
    {
        private static readonly string DbPath = AppDataPaths.ViewersDbFile;
        private string ConnectionString => $"Data Source={DbPath}";

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

            // GiveawayWinners table
            var winnersCmd = connection.CreateCommand();
            winnersCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS GiveawayWinners (
                    Id       INTEGER PRIMARY KEY AUTOINCREMENT,
                    WonAtUtc TEXT    NOT NULL,
                    Winner   TEXT    NOT NULL,
                    Prize    TEXT    NOT NULL DEFAULT '',
                    Amount   INTEGER NOT NULL DEFAULT 1,
                    Source   TEXT    NOT NULL DEFAULT ''
                );";
            await winnersCmd.ExecuteNonQueryAsync();

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

        // ── Giveaway Winners ─────────────────────────────────────────────────────

        public async Task AddWinnerAsync(GiveawayHistoryEntry entry)
        {
            await EnsureDatabaseCreatedAsync();

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO GiveawayWinners (WonAtUtc, Winner, Prize, Amount, Source)
                VALUES (@ts, @winner, @prize, @amount, @source);";
            command.Parameters.AddWithValue("@ts",     entry.TimestampUtc.ToString("O"));
            command.Parameters.AddWithValue("@winner", entry.Winner);
            command.Parameters.AddWithValue("@prize",  entry.Prize);
            command.Parameters.AddWithValue("@amount", entry.Amount);
            command.Parameters.AddWithValue("@source", entry.Source);
            await command.ExecuteNonQueryAsync();
        }

        public async Task BulkAddWinnersAsync(IEnumerable<GiveawayHistoryEntry> entries)
        {
            await EnsureDatabaseCreatedAsync();

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            using var tx = await connection.BeginTransactionAsync();
            foreach (var entry in entries)
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO GiveawayWinners (WonAtUtc, Winner, Prize, Amount, Source)
                    VALUES (@ts, @winner, @prize, @amount, @source);";
                command.Parameters.AddWithValue("@ts",     entry.TimestampUtc.ToString("O"));
                command.Parameters.AddWithValue("@winner", entry.Winner);
                command.Parameters.AddWithValue("@prize",  entry.Prize);
                command.Parameters.AddWithValue("@amount", entry.Amount);
                command.Parameters.AddWithValue("@source", entry.Source);
                await command.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }

        public async Task<List<GiveawayHistoryEntry>> GetWinnersAsync(string? search = null, int limit = 500)
        {
            await EnsureDatabaseCreatedAsync();

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            if (string.IsNullOrWhiteSpace(search))
            {
                command.CommandText = "SELECT WonAtUtc, Winner, Prize, Amount, Source FROM GiveawayWinners ORDER BY Id DESC LIMIT @limit";
                command.Parameters.AddWithValue("@limit", limit);
            }
            else
            {
                command.CommandText = @"SELECT WonAtUtc, Winner, Prize, Amount, Source FROM GiveawayWinners
                    WHERE Winner LIKE @q OR Prize LIKE @q
                    ORDER BY Id DESC LIMIT @limit";
                command.Parameters.AddWithValue("@q",     $"%{search.ToLowerInvariant()}%");
                command.Parameters.AddWithValue("@limit", limit);
            }

            var list = new List<GiveawayHistoryEntry>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new GiveawayHistoryEntry
                {
                    TimestampUtc = DateTime.TryParse(reader.GetString(0), out var dt) ? dt : DateTime.UtcNow,
                    Winner       = reader.GetString(1),
                    Prize        = reader.GetString(2),
                    Amount       = reader.GetInt64(3),
                    Source       = reader.GetString(4)
                });
            }
            return list;
        }

        public async Task<bool> ClearAllWinnersAsync()
        {
            await EnsureDatabaseCreatedAsync();

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM GiveawayWinners";
            await command.ExecuteNonQueryAsync();
            return true;
        }
    }
}
