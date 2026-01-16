using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Gw2Giveaway
{
    public class ItemInfo
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Rarity { get; set; } = "";
        public string? Description { get; set; }
    }

    public class SearchResult
    {
        public string Name { get; set; } = "";
        public string Rarity { get; set; } = "";
        public int Id { get; set; }
    }

    public static class Gw2ItemDatabase
    {
        public static Dictionary<int, ItemInfo> Items { get; private set; } = new Dictionary<int, ItemInfo>();

        private class ApiItem
        {
            public int id { get; set; }
            public string name { get; set; } = "";
            public string icon { get; set; } = "";
            public string rarity { get; set; } = "";
            public string? description { get; set; }
        }

        // Save next to the exe in a "Data" subfolder (creates if missing)
        private static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string DataFolder = Path.Combine(AppDir, "Data");
        private static readonly string CacheFile = Path.Combine(DataFolder, "items.json");

        public static async Task LoadAsync(HttpClient client, IProgress<int>? progress = null)
        {
            Directory.CreateDirectory(DataFolder);

            progress?.Report(0);

            if (File.Exists(CacheFile))
            {
                try
                {
                    string json = File.ReadAllText(CacheFile);
                    var cached = JsonSerializer.Deserialize<Dictionary<int, ItemInfo>>(json);
                    if (cached != null)
                    {
                        Items = cached;
                        progress?.Report(100);
                        return;
                    }
                }
                catch { /* corrupted – redownload */ }
            }

            string idsJson = await client.GetStringAsync("https://api.guildwars2.com/v2/items");
            List<int>? allIds = JsonSerializer.Deserialize<List<int>>(idsJson);

            if (allIds == null || allIds.Count == 0)
            {
                Items = new Dictionary<int, ItemInfo>();
                progress?.Report(100);
                return;
            }

            Items = new Dictionary<int, ItemInfo>();

            int total = allIds.Count;
            int processed = 0;

            for (int i = 0; i < allIds.Count; i += 200)
            {
                var chunk = allIds.Skip(i).Take(200).ToList();
                string idsParam = string.Join(",", chunk);
                string detailsJson = await client.GetStringAsync($"https://api.guildwars2.com/v2/items?ids={idsParam}");

                var apiItems = JsonSerializer.Deserialize<List<ApiItem>>(detailsJson) ?? new List<ApiItem>();

                foreach (var item in apiItems)
                {
                    Items[item.id] = new ItemInfo
                    {
                        Name = item.name,
                        Icon = item.icon,
                        Rarity = item.rarity,
                        Description = item.description
                    };
                }

                processed += chunk.Count;
                int percent = (int)((double)processed / total * 100);
                progress?.Report(percent);
            }

            try
            {
                string cacheJson = JsonSerializer.Serialize(Items);
                File.WriteAllText(CacheFile, cacheJson);
            }
            catch { /* ignore */ }

            progress?.Report(100);
        }

        public static List<SearchResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || Items.Count == 0)
                return new List<SearchResult>();

            string lowerQuery = query.Trim().ToLowerInvariant();

            var matches = Items
                .Where(kv => kv.Value.Name.ToLowerInvariant().Contains(lowerQuery))
                .Select(kv => new
                {
                    kv.Key,
                    kv.Value,
                    Score = GetMatchScore(kv.Value.Name.ToLowerInvariant(), lowerQuery)
                })
                .OrderBy(x => x.Score)
                .ThenBy(x => x.Value.Name)
                .Take(100)
                .Select(x => new SearchResult
                {
                    Name = x.Value.Name,
                    Rarity = x.Value.Rarity,
                    Id = x.Key
                })
                .ToList();

            return matches;
        }

        private static int GetMatchScore(string lowerName, string lowerQuery)
        {
            if (string.Equals(lowerName, lowerQuery)) return 0;
            if (lowerName.StartsWith(lowerQuery)) return 1;
            int index = lowerName.IndexOf(lowerQuery);
            return index >= 0 ? 2 + index : 1000;
        }
    }
}