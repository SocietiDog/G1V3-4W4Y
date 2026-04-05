using System;
using System.IO;

namespace Gw2Giveaway.Services
{
    public static class AppLogger
    {
        private static readonly object Sync = new();
        private static readonly string LogFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "logs");

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
    }
}
