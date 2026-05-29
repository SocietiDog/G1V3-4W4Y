using System.Text.Json.Serialization;

namespace Gw2Giveaway
{
    public class PrizeSlot
    {
        public int ItemId { get; set; } = 0;
        [JsonIgnore]
        public ItemInfo? Item { get; set; }
        public string? CustomName { get; set; }
        public string? CustomIconUrl { get; set; }
        public int DisplayStack { get; set; } = 0;
        public int GiveawayAmount { get; set; } = 1;

        // FIXED: Consistent with the enum name – no more GiveawayRarity
        public PrizeRarity PrizeRarity { get; set; } = PrizeRarity.Common;
    }

    public class PrizeWin
    {
        public bool IsGold { get; set; }
        public long WinAmount { get; set; }
        public ItemInfo? WinItem { get; set; }
        public string? CustomName { get; set; }
        public string? CustomIconUrl { get; set; }
        public int Row { get; set; } = -1;
        public int Col { get; set; } = -1;
    }

    public enum PrizeRarity
    {
        Common,
        Uncommon,
        Rare,
        SuperRare
    }

    public class PrizeBank
    {
        public PrizeSlot[,] Slots { get; set; }
        public long GoldAmount { get; set; } = 0;
        public int Rows { get; set; } = 3;
        public int Cols { get; set; } = 10;

        public PrizeBank() : this(3, 10) { }

        public PrizeBank(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            Slots = new PrizeSlot[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    Slots[r, c] = new PrizeSlot();
        }

        /// <summary>Resize the grid, preserving as many existing slots as fit.</summary>
        public void Resize(int newRows, int newCols)
        {
            var newSlots = new PrizeSlot[newRows, newCols];
            for (int r = 0; r < newRows; r++)
                for (int c = 0; c < newCols; c++)
                    newSlots[r, c] = (r < Rows && c < Cols) ? Slots[r, c] : new PrizeSlot();
            Slots = newSlots;
            Rows = newRows;
            Cols = newCols;
        }

        public void Hydrate()
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    var slot = Slots[r, c];
                    slot.Item = Gw2ItemDatabase.Items.TryGetValue(slot.ItemId, out var info) ? info : null;
                }
        }
        // 1. Add this method to PrizeBank class (consumes the won amount from the slot)
        public void ConsumePrize(PrizeWin win)
        {
            if (win.IsGold || win.Row < 0 || win.Col < 0)
                return; // Gold doesn't deplete

            var slot = Slots[win.Row, win.Col];

            // Safe cast – GiveawayAmount is always int (1-250) for items
            slot.DisplayStack -= (int)win.WinAmount;

            if (slot.DisplayStack <= 0)
            {
                slot.Item = null;
                slot.ItemId = 0;
                slot.CustomName = null;
                slot.CustomIconUrl = null;
                slot.DisplayStack = 0;
                slot.GiveawayAmount = 1;
                slot.PrizeRarity = PrizeRarity.Common;
            }
        }
        public PrizeWin? GetRandomPrize()
        {
            // Define weights (adjust these numbers to balance – higher = more common)
            var rarityWeights = new Dictionary<PrizeRarity, int>
            {
                { PrizeRarity.Common, 100 },
                { PrizeRarity.Uncommon, 30 },
                { PrizeRarity.Rare, 10 },
                { PrizeRarity.SuperRare, 1 }
            };

            var weightedPrizes = new List<(PrizeWin prize, int weight)>();

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    var slot = Slots[r, c];
                    bool hasPrize = slot.Item != null || !string.IsNullOrEmpty(slot.CustomName);
                    if (hasPrize && slot.GiveawayAmount > 0)
                    {
                        int weight = rarityWeights[slot.PrizeRarity]; // Consistent with property name
                        weightedPrizes.Add((new PrizeWin
                        {
                            IsGold = false,
                            WinAmount = slot.GiveawayAmount,
                            WinItem = slot.Item,
                            CustomName = slot.CustomName,
                            CustomIconUrl = slot.CustomIconUrl,
                            Row = r,
                            Col = c
                        }, weight));
                    }
                }
            }

            // Gold always has Common-level weight
            if (GoldAmount > 0)
            {
                weightedPrizes.Add((new PrizeWin
                {
                    IsGold = true,
                    WinAmount = GoldAmount
                }, rarityWeights[PrizeRarity.Common]));
            }

            if (weightedPrizes.Count == 0) return null;

            // Calculate total weight
            int totalWeight = weightedPrizes.Sum(p => p.weight);

            // Pick random
            Random rnd = new Random();
            int roll = rnd.Next(totalWeight);

            int cumulative = 0;
            foreach (var (prize, weight) in weightedPrizes)
            {
                cumulative += weight;
                if (roll < cumulative)
                    return prize;
            }

            return weightedPrizes.Last().prize; // fallback (should never hit)
        }
    }
}