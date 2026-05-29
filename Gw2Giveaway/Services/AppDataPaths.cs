using System;
using System.IO;

namespace Gw2Giveaway.Services
{
    public static class AppDataPaths
    {
        private static readonly string LocalAppDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Stable app folder name (no "Beta" suffix)
        public static string BaseFolder { get; } = Path.Combine(LocalAppDataRoot, "G1V3 - 4W4Y");

        public static string DataFolder => Path.Combine(BaseFolder, "Data");
        public static string LogsFolder => Path.Combine(DataFolder, "logs");
        public static string SettingsFile => Path.Combine(DataFolder, "settings.json");
        public static string BankFile => Path.Combine(DataFolder, "prizebank.json");
        public static string ItemsCacheFile => Path.Combine(DataFolder, "items.json");
        public static string ViewersDbFile => Path.Combine(BaseFolder, "viewers.db");

        private static string MigrationMarkerFile => Path.Combine(BaseFolder, ".legacy-migration-complete");

        /// <summary>
        /// One-time migration from legacy storage locations.
        /// On first run, if legacy files exist, they are copied into current storage (overwrite allowed),
        /// then legacy non-log data files are deleted to avoid confusion.
        /// </summary>
        public static void EnsureInitializedAndMigrateLegacyIfNeeded()
        {
            Directory.CreateDirectory(BaseFolder);
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(LogsFolder);

            if (File.Exists(MigrationMarkerFile))
                return;

            var legacyRoots = GetLegacyRoots().ToList();

            // Core files: choose newest legacy candidate and copy to destination (overwrite)
            MigrateNewestLegacyFile(legacyRoots.Select(r => Path.Combine(r, "Data", "settings.json")), SettingsFile);
            MigrateNewestLegacyFile(legacyRoots.Select(r => Path.Combine(r, "Data", "prizebank.json")), BankFile);
            MigrateNewestLegacyFile(legacyRoots.Select(r => Path.Combine(r, "Data", "items.json")), ItemsCacheFile);
            MigrateNewestLegacyFile(legacyRoots.Select(r => Path.Combine(r, "viewers.db")), ViewersDbFile);

            // Migrate any extra non-log files under legacy Data folders.
            foreach (var legacyRoot in legacyRoots)
            {
                string legacyDataFolder = Path.Combine(legacyRoot, "Data");
                if (!Directory.Exists(legacyDataFolder))
                    continue;

                foreach (var file in Directory.GetFiles(legacyDataFolder, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(legacyDataFolder, file);
                    if (relative.StartsWith("logs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(relative, "logs", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // keep logs in legacy locations
                    }

                    string target = Path.Combine(DataFolder, relative);
                    TryCopyOverwrite(file, target);
                    TryDeleteIfDestinationExists(file, target);
                }

                // Core safe cleanup (keep logs)
                TryDeleteIfDestinationExists(Path.Combine(legacyDataFolder, "settings.json"), SettingsFile);
                TryDeleteIfDestinationExists(Path.Combine(legacyDataFolder, "prizebank.json"), BankFile);
                TryDeleteIfDestinationExists(Path.Combine(legacyDataFolder, "items.json"), ItemsCacheFile);
                TryDeleteIfDestinationExists(Path.Combine(legacyRoot, "viewers.db"), ViewersDbFile);
            }

            TryWriteMigrationMarker();
        }

        private static IEnumerable<string> GetLegacyRoots()
        {
            var roots = new List<string>
            {
                AppDomain.CurrentDomain.BaseDirectory, // legacy install-folder storage
                Path.Combine(LocalAppDataRoot, "Gw2Giveaway"),
                Path.Combine(LocalAppDataRoot, "G1V3 - 4W4Y Beta")
            };

            return roots
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(r => !string.Equals(r.TrimEnd(Path.DirectorySeparatorChar), BaseFolder.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
        }

        private static void MigrateNewestLegacyFile(IEnumerable<string> candidates, string destination)
        {
            try
            {
                var source = candidates
                    .Where(File.Exists)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(fi => fi.LastWriteTimeUtc)
                    .FirstOrDefault();

                if (source == null)
                    return;

                TryCopyOverwrite(source.FullName, destination);
            }
            catch
            {
                // Non-fatal.
            }
        }

        private static void TryCopyOverwrite(string source, string destination)
        {
            try
            {
                if (!File.Exists(source))
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, overwrite: true);
            }
            catch
            {
                // Non-fatal.
            }
        }

        private static void TryDeleteIfDestinationExists(string source, string destination)
        {
            try
            {
                if (!File.Exists(source) || !File.Exists(destination))
                    return;

                File.Delete(source);
            }
            catch
            {
                // Non-fatal.
            }
        }

        private static void TryWriteMigrationMarker()
        {
            try
            {
                File.WriteAllText(MigrationMarkerFile, DateTime.UtcNow.ToString("O"));
            }
            catch
            {
                // Non-fatal.
            }
        }
    }
}
