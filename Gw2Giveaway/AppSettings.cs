using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;

namespace Gw2Giveaway
{
    public enum EntryMode
    {
        Command,
        AllChatters,
        ChannelPointManual  // Real PubSub later – manual add for now
    }

    public enum GiveawayMode
    {
        BankOnly,         // 0 - Only roll from bank
        PrizeOnly,        // 1 - Only roll from displayed prize with slot wheel
        RandomPool        // 2 - Slot wheel with 50/50 random between bank and prize
    }

    public class AppSettings
    {
        public string TwitchChannel { get; set; } = "";
        public string TwitchBotName { get; set; } = "";
        [JsonIgnore]
        public string TwitchOAuth { get; set; } = "";
        [JsonProperty("TwitchOAuth")]
        public string LegacyTwitchOAuth { get; set; } = "";
        public string TwitchOAuthEncrypted { get; set; } = "";
        public string TwitchClientId { get; set; } = "";
        public EntryMode EntryType { get; set; } = EntryMode.Command;
        public string EntryCommand { get; set; } = "!enter";
        public string ChannelPointTitle { get; set; } = "";
        public string ChatTemplate { get; set; } = "The winner is @{winner}! Congrats on winning {prize}!";
        public string WinnerPrizeTemplate { get; set; } = "{amount} × {prize}";
        public GiveawayMode CurrentGiveawayMode { get; set; } = GiveawayMode.RandomPool;
        public int RandomPoolBankPercentage { get; set; } = 50;  // 0-100% for bank vs prize in random mode
        public bool ShowPrizeRarityBadges { get; set; } = true;
        public string ClassicPrizeName { get; set; } = "Legendary Weapon";
        public string ClassicPrizeIconUrl { get; set; } = "";
        public bool ClassicUsePrizePool { get; set; } = false;
        public string ClassicPrizePool { get; set; } = "";
        public ObservableCollection<ChannelPointReward> ChannelPointRewards { get; set; } = new();
        public int SlotRollDurationSeconds { get; set; } = 12;  // Default: 12 seconds
        public int BankRollDurationSeconds { get; set; } = 5;   // Default: 5 seconds for bank highlight animation
        public bool AutoOpenOverlay { get; set; } = false;
        public string OverlayBackground { get; set; } = "#DD000000";
        public string OverlayInnerBackground { get; set; } = "#EE000000";
        public string OverlayBorderColor { get; set; } = "#FFD700";
        public string TitleColor { get; set; } = "#FFD700";
        public string HeaderColor { get; set; } = "#FFCC00";
        public string TextColor { get; set; } = "#FFFFFF";
        public string AccentColor { get; set; } = "#00FFAA";
        public string GoldColor { get; set; } = "#FFFF00";
        public int FirstCorrectReward { get; set; } = 150;
        public int LaterCorrectReward { get; set; } = 50;

        // Optional: Add BroadcasterId here too for channel points
        public string BroadcasterId { get; set; } = "";

    }
    public class ChannelPointReward
    {
        public string Title { get; set; } = "Channel Point Reward";
        public int Cost { get; set; } = 100; // Display only – info for streamer
        public ChannelPointAction Action { get; set; } = ChannelPointAction.AddToEntrants;
        public long InstantGoldAmount { get; set; } = 0; // If instant gold win
    }

    public enum ChannelPointAction
    {
        AddToEntrants,
        InstantBankRoll,
        InstantGoldWin
    }
}