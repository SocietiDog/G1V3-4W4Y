using System.IO;
using Newtonsoft.Json;

namespace Gw2Giveaway
{
    public enum EntryMode
    {
        Command,
        AllChatters,
        ChannelPointManual  // Real PubSub later – manual add for now
    }

    public enum RollMode
    {
        ClassicWheel,
        BankAnimation,
        BankRandom
    }

    public class AppSettings
    {
        public string TwitchChannel { get; set; } = "";
        public string TwitchBotName { get; set; } = "";
        public string TwitchOAuth { get; set; } = "";
        public EntryMode EntryType { get; set; } = EntryMode.Command;
        public string EntryCommand { get; set; } = "!enter";
        public string ChannelPointTitle { get; set; } = "";
        public string ChatTemplate { get; set; } = "The winner is @{winner}! Congrats on winning {prize}!";
        public string WinnerPrizeTemplate { get; set; } = "{amount} × {prize}";
        public RollMode CurrentRollMode { get; set; } = RollMode.ClassicWheel;
        public bool ShowPrizeRarityBadges { get; set; } = true;
        public string ClassicPrizeName { get; set; } = "Legendary Weapon";
        public string ClassicPrizeIconUrl { get; set; } = "";
    }
}