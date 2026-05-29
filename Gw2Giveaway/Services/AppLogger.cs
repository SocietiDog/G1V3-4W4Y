using System;
using System.IO;

namespace Gw2Giveaway.Services
{
    public static class AppLogger
    {
        private static readonly object Sync = new();
        private static readonly string LogFolder = AppDataPaths.LogsFolder;

        public static void LogError(string source, Exception ex)
        {
            Log($"ERROR [{source}] {ex}");
        }

        public static void LogInfo(string source, string message)
        {
            Log($"INFO [{source}] {message}");
        }

        private static void Log(string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(LogFolder);
                    string file = Path.Combine(LogFolder, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
                    File.AppendAllText(file, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
                }
            }
            catch
            {
            }
        }

        /// <summary>Deletes log files older than <paramref name="days"/> days. Safe to call on startup.</summary>
        public static void PruneOldLogs(int days = 30)
        {
            try
            {
                if (!Directory.Exists(LogFolder)) return;
                var cutoff = DateTime.UtcNow.AddDays(-days);
                foreach (var file in Directory.GetFiles(LogFolder, "*.log"))
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                        File.Delete(file);
                }
            }
            catch
            {
            }
        }
    }
}
