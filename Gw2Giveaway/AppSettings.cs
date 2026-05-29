using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;

namespace Gw2Giveaway
{
    public enum EntryMode
    {
        Command,
        ActiveUsers,
        Gw2Account,          // Players type their GW2 account name (e.g. Name.1234) to enter
        ChannelPointManual   // Real PubSub later – manual add for now
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
        public string TwitchBotName { get; set; } = "G1V3 - 4W4Y";
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
        public string ClassicPrizeName { get; set; } = "";
        public string ClassicPrizeIconUrl { get; set; } = "";
        public bool ClassicUsePrizePool { get; set; } = false;
        public string ClassicPrizePool { get; set; } = "";
        public ObservableCollection<ChannelPointReward> ChannelPointRewards { get; set; } = new();
        public int SlotRollDurationSeconds { get; set; } = 12;  // Default: 12 seconds
        public int BankRollDurationSeconds { get; set; } = 5;   // Default: 5 seconds for bank highlight animation
        public int BankRows { get; set; } = 3;
        public int BankColumns { get; set; } = 10;
        public bool AutoOpenOverlay { get; set; } = false;
        public double? OverlayLeft { get; set; }
        public double? OverlayTop { get; set; }
        public double? OverlayWidth { get; set; }
        public double? OverlayHeight { get; set; }
        public double? TriviaOverlayLeft { get; set; }
        public double? TriviaOverlayTop { get; set; }
        public double? TriviaOverlayWidth { get; set; }
        public double? TriviaOverlayHeight { get; set; }
        public double? PrizeBankLeft { get; set; }
        public double? PrizeBankTop { get; set; }
        public double? PrizeBankWidth { get; set; }
        public double? PrizeBankHeight { get; set; }
        public double? MainWindowLeft { get; set; }
        public double? MainWindowTop { get; set; }
        public double? MainWindowWidth { get; set; }
        public double? MainWindowHeight { get; set; }
        public double? TriviaSettingsLeft { get; set; }
        public double? TriviaSettingsTop { get; set; }
        public double? TriviaSettingsWidth { get; set; }
        public double? TriviaSettingsHeight { get; set; }
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
        public bool DisclaimerAccepted { get; set; } = false;
        public int DisclaimerAcceptedVersion { get; set; } = 0;

        public bool FollowersOnly { get; set; } = false;
        public int SubscriberBonusEntries { get; set; } = 1;  // 1 = no bonus; subs get this many entries

        // Optional: Add BroadcasterId here too for channel points
        public string BroadcasterId { get; set; } = "";

        // ── YouTube Beta (optional, can be disabled if not streaming on YT) ─────
        public bool YouTubeBetaEnabled { get; set; } = false;
        /// <summary>Full YouTube watch URL or bare 11-char video ID for the live stream.</summary>
        public string YouTubeVideoId { get; set; } = "";
        // ─────────────────────────────────────────────────────────────────────────

        public ObservableCollection<GiveawayHistoryEntry> GiveawayHistory { get; set; } = new();

    }

    public class GiveawayHistoryEntry
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string Winner { get; set; } = string.Empty;
        public string Prize { get; set; } = string.Empty;
        public long Amount { get; set; } = 1;
        public string Source { get; set; } = string.Empty;
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