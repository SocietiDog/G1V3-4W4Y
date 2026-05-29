using Gw2Giveaway.Services;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TwitchLib.Api;


namespace Gw2Giveaway
{
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }

        public ObservableCollection<string> Entrants { get; } = new();

        // Single shared TwitchChat instance (IRC)
        private readonly TwitchChat _twitch = new();
        public TwitchChat Twitch => _twitch; // Public read-only access if needed elsewhere

        // EventSub for channel points (separate modern connection)
        private readonly TwitchEventSub _eventSub = new();

        // ViewModels / Windows
        public TriviaViewModel Trivia { get; private set; } = null!; // Assigned in constructor

        private OverlayWindow? _overlay;
        private BankWindow? _bankWindow;
        private TriviaOverlayWindow? _triviaOverlay;

        private const string ShowOverlayButtonText = "Show Overlay";
        private const string HideOverlayButtonText = "Hide Overlay";
        private const string ShowTriviaOverlayButtonText = "Show Trivia Overlay";
        private const string HideTriviaOverlayButtonText = "Hide Trivia Overlay";
        private const string OpenPrizeBankButtonText = "Open Prize Bank Editor";
        private const string ClosePrizeBankButtonText = "Close Prize Bank Editor";

        private static bool IsValidWindowBounds(double? left, double? top, double? width, double? height)
            => left.HasValue && top.HasValue && width.HasValue && height.HasValue
               && !double.IsNaN(left.Value) && !double.IsInfinity(left.Value)
               && !double.IsNaN(top.Value) && !double.IsInfinity(top.Value)
               && !double.IsNaN(width.Value) && !double.IsInfinity(width.Value) && width.Value > 0
               && !double.IsNaN(height.Value) && !double.IsInfinity(height.Value) && height.Value > 0;

        private static void ApplySavedWindowBounds(Window window, double? left, double? top, double? width, double? height)
        {
            if (!IsValidWindowBounds(left, top, width, height))
                return;

            window.Left = left!.Value;
            window.Top = top!.Value;
            window.Width = width!.Value;
            window.Height = height!.Value;
        }

        private static bool TryCaptureWindowBounds(Window window, out double left, out double top, out double width, out double height)
        {
            Rect sourceBounds;

            if (window.WindowState == WindowState.Normal)
            {
                sourceBounds = new Rect(window.Left, window.Top, window.Width, window.Height);
            }
            else
            {
                sourceBounds = window.RestoreBounds;
            }

            left = sourceBounds.Left;
            top = sourceBounds.Top;
            width = sourceBounds.Width;
            height = sourceBounds.Height;

            return IsValidWindowBounds(left, top, width, height);
        }

        private bool _entriesOpen = false;
        private DispatcherTimer _entryTimer = new();
        private DispatcherTimer? _activeUsersPollTimer;
        private int _entryTimeSeconds = 300;

        // GW2 account name map: Twitch login -> GW2 account name
        private readonly Dictionary<string, string> _gw2AccountMap = new(StringComparer.OrdinalIgnoreCase);

        // Follower cache: Twitch login -> (isFollower, expiry)
        private readonly Dictionary<string, (bool IsFollower, DateTime Expiry)> _followerCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan FollowerCacheTtl = TimeSpan.FromMinutes(5);

        // Winner messages tab
        private string? _currentWinner;
        public ObservableCollection<WinnerChatMessage> WinnerMessages { get; } = new();

        // YouTube Beta
        private YouTubeChat? _youtube;

        public PrizeBank Bank { get; private set; } = new();

        private readonly HttpClient _httpClient = new();

        private const string DefaultTwitchClientId = "dlql0djuoozvkc81epya43ilibu19e";
        private const string TwitchOAuthRedirectUri = "http://localhost:54827/callback/";
        private const string TwitchOAuthScopes = "chat:read chat:edit channel:read:redemptions moderator:read:chatters moderator:read:followers";
        private const int ActiveUsersPollIntervalSeconds = 30;
        private const int CurrentDisclaimerVersion = 1;

        private static string AppDisplayVersion =>
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+', StringSplitOptions.RemoveEmptyEntries)[0]
            ?? "1.0.0";

        private static readonly string DisclaimerText = """
            Important Disclaimer

            Gw2Giveaway is a community helper tool for streamers running giveaways.

            - Giveaway prizes are provided by the streamer/host, not by this application.
            - You are solely responsible for your own giveaways, prizes, chat rules, and outcomes.
            - The app is provided "as is" with no warranties, guarantees, or legal advice.
            - The developer is not liable for lost items, missed rewards, user disputes, account actions, bans, data loss, hardware issues, or other damages.
            - Use of this app is at your own risk, including any impact on your PC, operating system, software, or network.
            - You are responsible for legal compliance, including giveaway laws, age restrictions, taxes, and regional requirements.
            - The app does not guarantee uptime, uninterrupted operation, fairness outcomes, or successful prize delivery.
            - You must follow Twitch Terms of Service and all applicable platform/game rules.
            - You are responsible for how participant/user data is collected, stored, and handled.
            - This project is not affiliated with, endorsed by, or sponsored by ArenaNet, Guild Wars 2, or Twitch.
            """;

        private static readonly string DataFolder = AppDataPaths.DataFolder;
        private static readonly string BankFile = AppDataPaths.BankFile;
        private static readonly string SettingsFile = AppDataPaths.SettingsFile;
        private static readonly string ViewersDbFile = AppDataPaths.ViewersDbFile;

        private AppSettings _settings = new();
        private string _oauthAccessToken = string.Empty;
        private string _oauthUserId = string.Empty;

        private ObservableCollection<ChannelPointReward> _channelPointRewards = new();
        public ObservableCollection<ChannelPointReward> ChannelPointRewards => _channelPointRewards;

        private readonly DatabaseService _databaseService = new();
        private readonly TwitchAPI _twitchApi = new();

        private ObservableCollection<GiveawayHistoryEntry> _giveawayHistory = new();

        private sealed class TriviaViewerRow
        {
            public string Username { get; set; } = string.Empty;
            public long Iq { get; set; }
        }

        public sealed class WinnerChatMessage
        {
            public string Sender { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
            public override string ToString() => $"{Sender}: {Text}";
        }

        /// <summary>Returns the GW2 account name for a Twitch user, or empty string if not set.</summary>
        public string GetGw2AccountName(string twitchLogin)
            => _gw2AccountMap.TryGetValue(twitchLogin, out var name) ? name : string.Empty;

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
            AppNameVersionText.Text = $"G1V3 - 4W4Y Beta v{AppDisplayVersion}";
            Loaded += MainWindow_Loaded;
            Closing += (s, e) =>
            {
                if (TryCaptureWindowBounds(this, out var left, out var top, out var width, out var height))
                {
                    _settings.MainWindowLeft = left;
                    _settings.MainWindowTop = top;
                    _settings.MainWindowWidth = width;
                    _settings.MainWindowHeight = height;
                    PersistSettings();
                }
            };

            try
            {
                if (EntrantsList != null)
                {
                    EntrantsList.ItemsSource = Entrants;
                }
                if (WinnerMessagesList != null)
                {
                    WinnerMessagesList.ItemsSource = WinnerMessages;
                }
                // Create TriviaViewModel with the shared TwitchChat instance
                Trivia = new TriviaViewModel(_twitch);

                try
                {
                    LoadSettings();
                    ApplySavedWindowBounds(this, _settings.MainWindowLeft, _settings.MainWindowTop, _settings.MainWindowWidth, _settings.MainWindowHeight);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("MainWindow.LoadSettings", ex);
                    _settings = new AppSettings();
                }

                SetChannelPointsAvailability(null);
                _ = CheckAndApplyChannelPointsAvailabilityAsync(_oauthAccessToken, _settings.TwitchClientId, BroadcasterIdText.Text.Trim());

                // Permanent event hooks (only once)
                _twitch.OnMessageReceived += Twitch_OnMessageReceived;

                _twitch.OnJoinedChannel += () =>
                {
                    Dispatcher.Invoke(async () =>
                    {
                        TwitchStatus.Text = "Connected & Joined!";
                        TwitchStatus.Foreground = Brushes.LimeGreen;
                        ConnectButton.IsEnabled = false;
                        DisconnectButton.IsEnabled = true;

                        string onlineName = !string.IsNullOrWhiteSpace(BotNameText.Text) ? BotNameText.Text.Trim() : _settings.TwitchBotName;
                        await _twitch.SendMessageAsync($"{onlineName} is online.");

                        // Connect EventSub for channel points
                        string broadcasterId = BroadcasterIdText.Text.Trim();

                        if (!string.IsNullOrWhiteSpace(broadcasterId))
                        {
                            await _eventSub.ConnectAsync(_twitch.OAuth, broadcasterId, _settings.TwitchClientId);
                        }
                    });
                };

                _twitch.OnConnectionError += error =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TwitchStatus.Text = "Connection failed";
                        TwitchStatus.Foreground = Brushes.Red;
                        DialogService.ShowInfo($"Twitch connection failed: {error}");
                        ConnectButton.IsEnabled = true;
                        DisconnectButton.IsEnabled = false;

                        Trivia.IsTriviaEnabled = false;
                    });
                };

                _eventSub.OnRewardRedeemed += (username, title, rewardId, input, redemptionId) =>
                {
                    Dispatcher.Invoke(async () =>
                    {
                        username = username.Trim().ToLowerInvariant();
                        title = title.Trim();

                        if (title.Contains("Trivia", StringComparison.OrdinalIgnoreCase))
                        {
                            if (Trivia.IsTriviaEnabled && !Trivia.IsTriviaPaused)
                            {
                                Trivia.StartTriviaRound();
                                await _twitch.SendMessageAsync($"🤖Trivia-Tron: Trivia started by {username}'s redemption!");
                            }
                        }

                        var reward = _channelPointRewards.FirstOrDefault(r =>
                            !string.IsNullOrWhiteSpace(r.Title) &&
                            string.Equals(r.Title.Trim(), title, StringComparison.OrdinalIgnoreCase));

                        if (reward == null)
                            return;

                        switch (reward.Action)
                        {
                            case ChannelPointAction.AddToEntrants:
                                AddEntrant(username);
                                await _twitch.SendMessageAsync($"@{username} redeemed {reward.Title} and joined the giveaway!");
                                break;

                            case ChannelPointAction.InstantBankRoll:
                                InstantBankRollForUser(username);
                                break;

                            case ChannelPointAction.InstantGoldWin:
                                if (reward.InstantGoldAmount > 0)
                                {
                                    await _twitch.SendMessageAsync($"@{username} redeemed {reward.Title} and won {reward.InstantGoldAmount} Gold!");
                                }
                                break;
                        }
                    });
                };

                DataContext = this;

                _ = LoadDatabaseWithProgress();
                _ = RefreshDataManagementViewAsync();
                _ = RefreshViewerCountAsync();

                if (_settings.AutoOpenOverlay)
                    ShowTriviaOverlay();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MainWindow.Constructor", ex);
                DialogService.ShowInfo($"Startup error: {ex.Message}", "Gw2Giveaway");
                DataContext = this;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (EnsureDisclaimerAccepted())
                return;

            Application.Current.Shutdown();
        }

        private bool EnsureDisclaimerAccepted()
        {
            if (_settings.DisclaimerAccepted && _settings.DisclaimerAcceptedVersion >= CurrentDisclaimerVersion)
                return true;

            var dialog = new DisclaimerDialog($"Disclaimer & Terms • Beta v{AppDisplayVersion}", DisclaimerText, requireAcceptance: true);
            dialog.ShowDialog();

            if (!dialog.Accepted)
            {
                DialogService.ShowInfo("You must accept the disclaimer to use Gw2Giveaway.", "Disclaimer Required");
                return false;
            }

            _settings.DisclaimerAccepted = true;
            _settings.DisclaimerAcceptedVersion = CurrentDisclaimerVersion;
            PersistSettings();
            return true;
        }

        private void LoadSettings()
        {
            _settings = new AppSettings(); // defaults

            if (File.Exists(SettingsFile))
            {
                try
                {
                    string json = File.ReadAllText(SettingsFile);
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("LoadSettings.Deserialize", ex);
                }
            }

            if (string.IsNullOrWhiteSpace(_settings.TwitchOAuth) && !string.IsNullOrWhiteSpace(_settings.TwitchOAuthEncrypted))
            {
                try
                {
                    _settings.TwitchOAuth = SecureTokenProtector.Unprotect(_settings.TwitchOAuthEncrypted);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("LoadSettings.UnprotectOAuth", ex);
                    _settings.TwitchOAuth = string.Empty;
                }
            }

            _settings.TwitchClientId = DefaultTwitchClientId;

            if (string.IsNullOrWhiteSpace(_settings.TwitchBotName))
                _settings.TwitchBotName = "G1V3 - 4W4Y";

            if (EntryModeCombo != null)
            {
                int entryIndex = (int)_settings.EntryType;
                if (entryIndex < 0 || entryIndex >= EntryModeCombo.Items.Count)
                    entryIndex = 0;
                EntryModeCombo.SelectedIndex = entryIndex;
            }

            _channelPointRewards = _settings.ChannelPointRewards != null
                ? new ObservableCollection<ChannelPointReward>(_settings.ChannelPointRewards.Where(r => r != null).Select(r => new ChannelPointReward
                {
                    Title = r!.Title,
                    Cost = r.Cost,
                    Action = r.Action,
                    InstantGoldAmount = r.InstantGoldAmount
                }))
                : new ObservableCollection<ChannelPointReward>();

            // Migrate legacy JSON winner history into the DB (one-time)
            if (_settings.GiveawayHistory != null && _settings.GiveawayHistory.Count > 0)
            {
                var toMigrate = _settings.GiveawayHistory.Where(h => h != null).ToList();
                _ = _databaseService.BulkAddWinnersAsync(toMigrate);
                _settings.GiveawayHistory.Clear();
            }
            _giveawayHistory = new ObservableCollection<GiveawayHistoryEntry>();

            // Giveaway settings
            ChannelText.Text = _settings.TwitchChannel;
            BotNameText.Text = _settings.TwitchBotName;
            _oauthAccessToken = _settings.TwitchOAuth;
            BroadcasterIdText.Text = _settings.BroadcasterId;
            if (EntryModeCombo != null)
            {
                int entryIndex = (int)_settings.EntryType;
                if (entryIndex < 0 || entryIndex >= EntryModeCombo.Items.Count)
                    entryIndex = 0;
                EntryModeCombo.SelectedIndex = entryIndex;
            }
            EntryCommandText.Text = _settings.EntryCommand;
            UpdateEntryCommandFieldState();
            if (FollowersOnlyCheck != null)
                FollowersOnlyCheck.IsChecked = _settings.FollowersOnly;
            if (SubBonusEntriesText != null)
                SubBonusEntriesText.Text = _settings.SubscriberBonusEntries.ToString();

            // YouTube Beta
            if (YouTubeBetaEnabledCheck != null)
                YouTubeBetaEnabledCheck.IsChecked = _settings.YouTubeBetaEnabled;
            if (YouTubeVideoIdText != null)
                YouTubeVideoIdText.Text = _settings.YouTubeVideoId;
            if (YouTubeBetaPanel != null)
                YouTubeBetaPanel.IsEnabled = _settings.YouTubeBetaEnabled;
            ChatTemplateText.Text = _settings.ChatTemplate;
            WinnerPrizeTemplateText.Text = _settings.WinnerPrizeTemplate;
            GiveawayModeCombo.SelectedIndex = (int)_settings.CurrentGiveawayMode;
            BankPercentageSlider.Value = _settings.RandomPoolBankPercentage;
            UpdateBankPercentageDisplay();
            ShowRarityBadgesCheck.IsChecked = _settings.ShowPrizeRarityBadges;
            ShowRarityBadgesCheck2.IsChecked = _settings.ShowPrizeRarityBadges;
            SlotRollDurationSlider.Value = _settings.SlotRollDurationSeconds;
            BankRollDurationSlider.Value = _settings.BankRollDurationSeconds;
            UpdateSlotRollDurationDisplay();
            UpdateBankRollDurationDisplay();
            ClassicPrizePoolText.Text = _settings.ClassicPrizePool;
            UpdateClassicPoolPreviewUi();

            // Show/hide random pool panel based on mode
            if (_settings.CurrentGiveawayMode == GiveawayMode.RandomPool)
                RandomPoolPanel.Visibility = Visibility.Visible;
            else
                RandomPoolPanel.Visibility = Visibility.Collapsed;

            // Trivia settings (direct – no prefix, same names as ViewModel)
            if (Trivia != null)
            {
                Trivia.AutoOpenOverlay = _settings.AutoOpenOverlay;
                Trivia.OverlayBackground = _settings.OverlayBackground;
                Trivia.OverlayInnerBackground = _settings.OverlayInnerBackground;
                Trivia.OverlayBorderColor = _settings.OverlayBorderColor;
                Trivia.TitleColor = _settings.TitleColor;
                Trivia.HeaderColor = _settings.HeaderColor;
                Trivia.TextColor = _settings.TextColor;
                Trivia.AccentColor = _settings.AccentColor;
                Trivia.GoldColor = _settings.GoldColor;
                Trivia.FirstCorrectReward = _settings.FirstCorrectReward;
                Trivia.LaterCorrectReward = _settings.LaterCorrectReward;
            }
        }

        public void SaveSettings()
        {
            // Giveaway
            _settings.TwitchChannel = ChannelText.Text.Trim();
            _settings.TwitchBotName = BotNameText.Text.Trim();
            _settings.TwitchOAuth = string.Empty;
            _settings.LegacyTwitchOAuth = string.Empty;
            try
            {
                _settings.TwitchOAuthEncrypted = SecureTokenProtector.Protect(_oauthAccessToken.Trim());
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SaveSettings.ProtectOAuth", ex);
                _settings.TwitchOAuthEncrypted = string.Empty;
            }
            _settings.TwitchClientId = string.IsNullOrWhiteSpace(_settings.TwitchClientId) ? DefaultTwitchClientId : _settings.TwitchClientId;
            _settings.BroadcasterId = BroadcasterIdText.Text.Trim();
            _settings.EntryType = (EntryMode)Math.Clamp(EntryModeCombo.SelectedIndex, 0, 2);
            _settings.EntryCommand = EntryCommandText.Text.Trim();
            _settings.FollowersOnly = FollowersOnlyCheck?.IsChecked == true;
            if (int.TryParse(SubBonusEntriesText?.Text?.Trim(), out int bonusEntries) && bonusEntries >= 1)
                _settings.SubscriberBonusEntries = bonusEntries;

            // YouTube Beta
            _settings.YouTubeBetaEnabled = YouTubeBetaEnabledCheck?.IsChecked == true;
            _settings.YouTubeVideoId = YouTubeVideoIdText?.Text?.Trim() ?? string.Empty;
            _settings.ChatTemplate = ChatTemplateText.Text;
            _settings.WinnerPrizeTemplate = WinnerPrizeTemplateText.Text;
            _settings.ShowPrizeRarityBadges = ShowRarityBadgesCheck.IsChecked == true;
            _settings.SlotRollDurationSeconds = (int)SlotRollDurationSlider.Value;
            _settings.BankRollDurationSeconds = (int)BankRollDurationSlider.Value;
            _settings.ClassicPrizePool = ClassicPrizePoolText.Text;
            var firstClassicEntry = ParseClassicPrizePoolEntries(_settings.ClassicPrizePool).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstClassicEntry.Name))
            {
                _settings.ClassicPrizeName = firstClassicEntry.Name;
                _settings.ClassicPrizeIconUrl = firstClassicEntry.IconUrl;
            }
            _settings.ClassicUsePrizePool = !string.IsNullOrWhiteSpace(_settings.ClassicPrizePool)
                || !string.IsNullOrWhiteSpace(_settings.ClassicPrizeName);
            _settings.ChannelPointRewards = new ObservableCollection<ChannelPointReward>(_channelPointRewards.Select(r => new ChannelPointReward
            {
                Title = r.Title,
                Cost = r.Cost,
                Action = r.Action,
                InstantGoldAmount = r.InstantGoldAmount
            }));
            // Winner history is now stored in viewers.db — not in settings.json
            _settings.GiveawayHistory.Clear();

            // Giveaway mode settings
            _settings.CurrentGiveawayMode = (GiveawayMode)GiveawayModeCombo.SelectedIndex;
            _settings.RandomPoolBankPercentage = (int)BankPercentageSlider.Value;

            // Trivia (direct – same names)
            _settings.AutoOpenOverlay = Trivia.AutoOpenOverlay;
            _settings.OverlayBackground = Trivia.OverlayBackground;
            _settings.OverlayInnerBackground = Trivia.OverlayInnerBackground;
            _settings.OverlayBorderColor = Trivia.OverlayBorderColor;
            _settings.TitleColor = Trivia.TitleColor;
            _settings.HeaderColor = Trivia.HeaderColor;
            _settings.TextColor = Trivia.TextColor;
            _settings.AccentColor = Trivia.AccentColor;
            _settings.GoldColor = Trivia.GoldColor;
            _settings.FirstCorrectReward = Trivia.FirstCorrectReward;
            _settings.LaterCorrectReward = Trivia.LaterCorrectReward;

            _overlay?.UpdateEntryInstruction(EntryCommandText.Text);
            UpdateOverlayToggleButtons();

            try
            {
                PersistSettings();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SaveSettings.WriteFile", ex);
            }
        }

        private void PersistSettings()
        {
            string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            Directory.CreateDirectory(DataFolder);
            File.WriteAllText(SettingsFile, json);
        }
        private void ShowTriviaOverlay_Click(object sender, RoutedEventArgs e) => ShowTriviaOverlay();

        private void ShowTriviaOverlay()
        {
            if (_triviaOverlay == null)
            {
                _triviaOverlay = new TriviaOverlayWindow(Trivia);
                ApplySavedWindowBounds(_triviaOverlay, _settings.TriviaOverlayLeft, _settings.TriviaOverlayTop, _settings.TriviaOverlayWidth, _settings.TriviaOverlayHeight);
                _triviaOverlay.IsVisibleChanged += TriviaOverlay_IsVisibleChanged;
                _triviaOverlay.Closed += (s, e) =>
                {
                    if (TryCaptureWindowBounds(_triviaOverlay, out var left, out var top, out var width, out var height))
                    {
                        _settings.TriviaOverlayLeft = left;
                        _settings.TriviaOverlayTop = top;
                        _settings.TriviaOverlayWidth = width;
                        _settings.TriviaOverlayHeight = height;
                        PersistSettings();
                    }

                    _triviaOverlay = null;
                    UpdateOverlayToggleButtons();
                };
            }

            if (_triviaOverlay.IsVisible)
                _triviaOverlay.Close();
            else
                _triviaOverlay.Show();

            UpdateOverlayToggleButtons();
        }
       
        private void OpenTriviaSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(Trivia, this);
            ApplySavedWindowBounds(settingsWindow,
                _settings.TriviaSettingsLeft,
                _settings.TriviaSettingsTop,
                _settings.TriviaSettingsWidth,
                _settings.TriviaSettingsHeight);

            settingsWindow.ShowDialog();

            if (TryCaptureWindowBounds(settingsWindow, out var left, out var top, out var width, out var height))
            {
                _settings.TriviaSettingsLeft = left;
                _settings.TriviaSettingsTop = top;
                _settings.TriviaSettingsWidth = width;
                _settings.TriviaSettingsHeight = height;
                PersistSettings();
            }
        }
        private async Task LoadDatabaseWithProgress(bool forceRefresh = false)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                DatabaseStatus.Text = forceRefresh ? "Refreshing database..." : "Loading database...";
                DatabaseProgress.Value = 0;
            });

            var progress = new Progress<int>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    DatabaseProgress.Value = p;
                    if (p < 100)
                        DatabaseStatus.Text = $"Loading: {p}%";
                });
            });

            try
            {
                await Gw2ItemDatabase.LoadAsync(_httpClient, progress);

                await Dispatcher.InvokeAsync(() =>
                {
                    DatabaseProgress.Value = 100;
                    DatabaseStatus.Text = $"Ready ({Gw2ItemDatabase.Items.Count:N0} items)";
                    UpdateClassicPoolPreviewUi();
                    UpdateOverlayClassicPrizePreview();
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("LoadDatabaseWithProgress", ex);
                await Dispatcher.InvokeAsync(() =>
                {
                    DatabaseProgress.Value = 0;
                    DatabaseStatus.Text = "Load failed";
                    DialogService.ShowInfo("Database error: " + ex.Message + "\nCheck internet connection.");
                });
            }
        }

        private async void RefreshDatabase_Click(object sender, RoutedEventArgs e)
        {
            await LoadDatabaseWithProgress(true);
        }

        private void ViewDisclaimer_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DisclaimerDialog($"Disclaimer & Terms • v{AppDisplayVersion}", DisclaimerText, requireAcceptance: false);
            dialog.ShowDialog();
        }

        private void LoadBankFromFile()
        {
            int savedRows = _settings.BankRows > 0 ? _settings.BankRows : 3;
            int savedCols = _settings.BankColumns > 0 ? _settings.BankColumns : 10;

            if (!File.Exists(BankFile))
            {
                Bank = new PrizeBank(savedRows, savedCols);
                SaveBank(); // brand-new install/run only
                return;
            }

            try
            {
                string json = File.ReadAllText(BankFile);
                var loaded = JsonConvert.DeserializeObject<PrizeBank>(json);
                if (loaded == null)
                {
                    AppLogger.LogInfo("LoadBankFromFile", $"Bank file exists but deserialized null. Keeping existing file intact: {BankFile}");
                    DialogService.ShowInfo("Failed to read prize bank data. The existing bank file was NOT changed.");
                    Bank = new PrizeBank(savedRows, savedCols);
                    Bank.Hydrate();
                    return;
                }

                Bank = loaded;

                // Ensure Rows/Cols are consistent with the actual array dimensions
                int actualRows = Bank.Slots?.GetLength(0) ?? 0;
                int actualCols = Bank.Slots?.GetLength(1) ?? 0;
                if (Bank.Slots == null || actualRows == 0 || actualCols == 0)
                {
                    AppLogger.LogInfo("LoadBankFromFile", $"Bank file has invalid slot matrix. Keeping existing file intact: {BankFile}");
                    DialogService.ShowInfo("Prize bank data is invalid. The existing bank file was NOT changed.");
                    Bank = new PrizeBank(savedRows, savedCols);
                    Bank.Hydrate();
                    return;
                }

                // Sync Rows/Cols in case this is an old save without them
                Bank.Rows = actualRows;
                Bank.Cols = actualCols;
                _settings.BankRows = actualRows;
                _settings.BankColumns = actualCols;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("LoadBankFromFile", ex);
                DialogService.ShowInfo("Failed to load prize bank. The existing bank file was NOT changed.\n" + ex.Message);
                Bank = new PrizeBank(savedRows, savedCols);
                Bank.Hydrate();
                return;
            }

            Bank.Hydrate();
        }

        private void SaveBank()
        {
            try
            {
                // Keep settings in sync with the actual bank dimensions
                _settings.BankRows = Bank.Rows;
                _settings.BankColumns = Bank.Cols;
                Directory.CreateDirectory(DataFolder);
                string json = JsonConvert.SerializeObject(Bank, Formatting.Indented);
                File.WriteAllText(BankFile, json);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SaveBank", ex);
                DialogService.ShowInfo("Save failed: " + ex.Message + "\nPath: " + BankFile);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            SaveBank();

            _triviaOverlay?.Close();
            _overlay?.Close();
            _bankWindow?.Close();
            _youtube?.Dispose();

            base.OnClosing(e);
        }

        // ── YouTube Beta handlers ──────────────────────────────────────────────────
        private void YouTubeBetaEnabledCheck_Changed(object sender, RoutedEventArgs e)
        {
            bool enabled = YouTubeBetaEnabledCheck?.IsChecked == true;
            if (YouTubeBetaPanel != null)
                YouTubeBetaPanel.IsEnabled = enabled;

            // If disabled while connected, disconnect
            if (!enabled && _youtube != null)
            {
                _youtube.Disconnect();
                YouTubeStatus.Text = "Not connected";
                YouTubeStatus.Foreground = Brushes.IndianRed;
            }
        }

        private async void ConnectYouTube_Click(object sender, RoutedEventArgs e)
        {
            string videoId = YouTubeVideoIdText?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(videoId))
            {
                DialogService.ShowInfo("Paste a YouTube live video ID or URL first.", "YouTube Beta");
                return;
            }

            // Dispose any previous instance
            _youtube?.Dispose();
            _youtube = new YouTubeChat();

            _youtube.OnStatusChanged += status =>
                Dispatcher.Invoke(() =>
                {
                    YouTubeStatus.Text = status;
                    YouTubeStatus.Foreground = status.StartsWith("✅") ? Brushes.LimeGreen
                        : status.StartsWith("⚠") ? Brushes.Orange
                        : Brushes.White;
                });

            _youtube.OnMessageReceived += (username, message, isMember) =>
                Twitch_OnMessageReceived(username, message, isMember);

            YouTubeStatus.Text = "Connecting…";
            YouTubeStatus.Foreground = Brushes.Orange;

            await _youtube.ConnectAsync(videoId);
        }

        private void DisconnectYouTube_Click(object sender, RoutedEventArgs e)
        {
            _youtube?.Disconnect();
            YouTubeStatus.Text = "Not connected";
            YouTubeStatus.Foreground = Brushes.IndianRed;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void ShowRarityBadgesCheck_Changed(object sender, RoutedEventArgs e)
        {
            // Guard against firing during XAML initialization before both controls exist
            if (ShowRarityBadgesCheck == null || ShowRarityBadgesCheck2 == null) return;

            // Determine the new value from whichever checkbox triggered
            bool show = (sender == ShowRarityBadgesCheck2)
                ? ShowRarityBadgesCheck2.IsChecked == true
                : ShowRarityBadgesCheck.IsChecked == true;

            // Keep both in sync without re-firing events
            if (ShowRarityBadgesCheck.IsChecked != show)
                ShowRarityBadgesCheck.IsChecked = show;
            if (ShowRarityBadgesCheck2.IsChecked != show)
                ShowRarityBadgesCheck2.IsChecked = show;

            // Update the open Bank window if it exists
            if (_bankWindow != null)
                _bankWindow.UpdateShowRarityBadges(show);
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async Task RefreshViewerCountAsync()
        {
            try
            {
                string broadcasterId = BroadcasterIdText.Text.Trim();
                string clientId = _settings.TwitchClientId;
                string token = _oauthAccessToken;

                if (string.IsNullOrWhiteSpace(broadcasterId) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(token))
                {
                    ViewerCountText.Text = "--";
                    return;
                }

                _twitchApi.Settings.ClientId = clientId;
                _twitchApi.Settings.AccessToken = token.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase) ? token["oauth:".Length..] : token;

                var response = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string> { broadcasterId });
                var stream = response.Streams.FirstOrDefault();
                ViewerCountText.Text = stream != null ? stream.ViewerCount.ToString() : "Offline";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MainWindow.RefreshViewerCountAsync", ex);
                ViewerCountText.Text = "--";
            }
        }

        private async void RefreshViewerCount_Click(object sender, RoutedEventArgs e)
        {
            await RefreshViewerCountAsync();
        }

        private async void AuthWithTwitch_Click(object sender, RoutedEventArgs e)
        {
            string clientId = ResolveTwitchClientId();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                DialogService.ShowInfo("Twitch login is not configured yet. Set TWITCH_CLIENT_ID (or save TwitchClientId in settings.json), then click Login with Twitch.");
                return;
            }

            AuthorizeButton.IsEnabled = false;
            TwitchStatus.Text = "Authorizing...";
            TwitchStatus.Foreground = Brushes.Orange;

            try
            {
                var token = await AuthenticateWithTwitchAsync(clientId);
                _oauthAccessToken = token.AccessToken;

                var profile = await GetTwitchProfileAsync(token.AccessToken);
                if (profile != null)
                {
                    _oauthUserId = profile.Value.UserId;

                    if (string.IsNullOrWhiteSpace(ChannelText.Text))
                        ChannelText.Text = profile.Value.Login;

                    if (string.IsNullOrWhiteSpace(BotNameText.Text))
                        BotNameText.Text = profile.Value.Login;

                    if (string.IsNullOrWhiteSpace(BroadcasterIdText.Text))
                        BroadcasterIdText.Text = profile.Value.UserId;
                }

                SaveSettings();
                await CheckAndApplyChannelPointsAvailabilityAsync(token.AccessToken, _settings.TwitchClientId, BroadcasterIdText.Text.Trim());

                TwitchStatus.Text = "Authorized";
                TwitchStatus.Foreground = Brushes.LimeGreen;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("AuthWithTwitch_Click", ex);
                TwitchStatus.Text = "Authorization failed";
                TwitchStatus.Foreground = Brushes.Red;
                DialogService.ShowInfo($"Twitch authorization failed: {ex.Message}");
            }
            finally
            {
                AuthorizeButton.IsEnabled = true;
            }
        }

        private string ResolveTwitchClientId()
        {
            _settings.TwitchClientId = DefaultTwitchClientId;
            SaveSettings();
            return _settings.TwitchClientId;
        }

        private async Task<TwitchOAuthToken> AuthenticateWithTwitchAsync(string clientId)
        {
            string state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

            string authorizeUrl =
                $"https://id.twitch.tv/oauth2/authorize?response_type=token&client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(TwitchOAuthRedirectUri)}&scope={Uri.EscapeDataString(TwitchOAuthScopes)}&state={Uri.EscapeDataString(state)}";

            using var listener = new HttpListener();
            listener.Prefixes.Add(TwitchOAuthRedirectUri);
            listener.Prefixes.Add("http://localhost:54827/callback/token/");
            listener.Start();

            Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });

            string accessToken = await WaitForImplicitAccessTokenAsync(listener, state);
            listener.Stop();

            return new TwitchOAuthToken(accessToken, string.Empty);
        }

        private static async Task<string> WaitForImplicitAccessTokenAsync(HttpListener listener, string expectedState)
        {
            DateTime deadline = DateTime.UtcNow.AddMinutes(3);

            while (DateTime.UtcNow < deadline)
            {
                TimeSpan remaining = deadline - DateTime.UtcNow;
                Task<HttpListenerContext> callbackTask = listener.GetContextAsync();
                Task timeoutTask = Task.Delay(remaining);

                Task completed = await Task.WhenAny(callbackTask, timeoutTask);
                if (completed != callbackTask)
                    break;

                HttpListenerContext context = await callbackTask;
                string path = context.Request.Url?.AbsolutePath?.TrimEnd('/').ToLowerInvariant() ?? string.Empty;

                if (path == "/callback")
                {
                    await WriteOAuthFragmentBridgePageAsync(context.Response);
                    continue;
                }

                if (path == "/token" || path == "/callback/token")
                {
                    string? error = context.Request.QueryString["error"];
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        await WriteOAuthFailurePageAsync(context.Response, error);
                        throw new InvalidOperationException($"Twitch returned error: {error}");
                    }

                    string? state = context.Request.QueryString["state"];
                    if (!string.Equals(expectedState, state, StringComparison.Ordinal))
                    {
                        await WriteOAuthFailurePageAsync(context.Response, "state_mismatch");
                        throw new InvalidOperationException("OAuth state validation failed.");
                    }

                    string? accessToken = context.Request.QueryString["access_token"];
                    if (string.IsNullOrWhiteSpace(accessToken))
                    {
                        await WriteOAuthFailurePageAsync(context.Response, "missing_access_token");
                        throw new InvalidOperationException("Access token was not returned by Twitch.");
                    }

                    await WriteOAuthCallbackResponseAsync(context.Response);
                    return accessToken;
                }

                context.Response.StatusCode = 404;
                context.Response.Close();
            }

            throw new TimeoutException("Timed out waiting for Twitch login callback.");
        }

        private static async Task WriteOAuthFragmentBridgePageAsync(HttpListenerResponse response)
        {
            const string html = "<html><body style='font-family:Segoe UI;background:#111;color:#fff;padding:24px;'><h2>Finishing Twitch sign-in...</h2><script>const h=(location.hash||'').replace(/^#/, '');if(h){location.replace('/callback/token/?'+h);}else{document.body.innerHTML+='<p>Missing token in redirect.</p>';}</script></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.StatusCode = 200;
            response.ContentType = "text/html";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
            response.Close();
        }

        private static async Task WriteOAuthFailurePageAsync(HttpListenerResponse response, string error)
        {
            string html = $"<html><body style='font-family:Segoe UI;background:#111;color:#fff;padding:24px;'><h2>Twitch sign-in failed</h2><p>Error: {WebUtility.HtmlEncode(error)}</p></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.StatusCode = 200;
            response.ContentType = "text/html";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
            response.Close();
        }

        private static async Task WriteOAuthCallbackResponseAsync(HttpListenerResponse response)
        {
            const string html = "<html><body style='font-family:Segoe UI;background:#111;color:#fff;padding:24px;'><h2>Gw2Giveaway authorized ✅</h2><p>You can close this tab and return to the app.</p></body></html>";

            byte[] buffer = Encoding.UTF8.GetBytes(html);
            response.StatusCode = 200;
            response.ContentType = "text/html";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
            response.Close();
        }

        private async Task<(string Login, string UserId)?> GetTwitchProfileAsync(string accessToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
            request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            string body = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(body);

            string login = doc.RootElement.TryGetProperty("login", out JsonElement loginEl)
                ? (loginEl.GetString() ?? string.Empty)
                : string.Empty;
            string userId = doc.RootElement.TryGetProperty("user_id", out JsonElement userEl)
                ? (userEl.GetString() ?? string.Empty)
                : string.Empty;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(userId))
                return null;

            return (login.ToLowerInvariant(), userId);
        }

        private async Task<HashSet<string>> GetCurrentChattersAsync()
        {
            var chatters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string broadcasterId = BroadcasterIdText.Text.Trim();
            string moderatorId = _oauthUserId?.Trim() ?? string.Empty;
            string clientId = _settings.TwitchClientId;
            string token = _oauthAccessToken;

            if (string.IsNullOrWhiteSpace(broadcasterId) ||
                string.IsNullOrWhiteSpace(moderatorId) ||
                string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(token))
            {
                return chatters;
            }

            string normalizedToken = token.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase)
                ? token["oauth:".Length..]
                : token;

            string? cursor = null;

            do
            {
                string url = $"https://api.twitch.tv/helix/chat/chatters?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&moderator_id={Uri.EscapeDataString(moderatorId)}&first=1000";
                if (!string.IsNullOrWhiteSpace(cursor))
                    url += $"&after={Uri.EscapeDataString(cursor)}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", normalizedToken);
                request.Headers.Add("Client-Id", clientId);

                using HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    AppLogger.LogError("GetCurrentChattersAsync.Http", new Exception($"Twitch chatters request failed: {(int)response.StatusCode}"));
                    return chatters;
                }

                string body = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(body);

                if (doc.RootElement.TryGetProperty("data", out JsonElement dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement chatter in dataEl.EnumerateArray())
                    {
                        if (chatter.TryGetProperty("user_login", out JsonElement loginEl))
                        {
                            string login = loginEl.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(login))
                                chatters.Add(login.ToLowerInvariant());
                        }
                    }
                }

                cursor = null;
                if (doc.RootElement.TryGetProperty("pagination", out JsonElement pageEl) &&
                    pageEl.ValueKind == JsonValueKind.Object &&
                    pageEl.TryGetProperty("cursor", out JsonElement cursorEl))
                {
                    cursor = cursorEl.GetString();
                }
            }
            while (!string.IsNullOrWhiteSpace(cursor));

            return chatters;
        }

        private async Task ImportCurrentChattersAsync(bool announceErrors)
        {
            try
            {
                var chatters = await GetCurrentChattersAsync();
                Dispatcher.Invoke(() =>
                {
                    foreach (string chatter in chatters)
                    {
                        if (!Entrants.Contains(chatter))
                            Entrants.Add(chatter);
                    }
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ImportCurrentChattersAsync", ex);
                if (announceErrors)
                    DialogService.ShowInfo("Couldn't fetch full chatter list. Active Users will continue adding message senders.");
            }
        }

        private void StartActiveUsersPolling()
        {
            StopActiveUsersPolling();

            _activeUsersPollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(ActiveUsersPollIntervalSeconds)
            };
            _activeUsersPollTimer.Tick += async (_, _) =>
            {
                if (_entriesOpen && _settings.EntryType == EntryMode.ActiveUsers)
                    await ImportCurrentChattersAsync(announceErrors: false);
            };
            _activeUsersPollTimer.Start();
        }

        private void StopActiveUsersPolling()
        {
            if (_activeUsersPollTimer == null)
                return;

            _activeUsersPollTimer.Stop();
            _activeUsersPollTimer = null;
        }

        private async Task CheckAndApplyChannelPointsAvailabilityAsync(string accessToken, string clientId, string broadcasterId)
        {
            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(broadcasterId))
            {
                SetChannelPointsAvailability(null);
                return;
            }

            string? broadcasterType = await GetBroadcasterTypeAsync(accessToken, clientId, broadcasterId);
            SetChannelPointsAvailability(broadcasterType);
        }

        private async Task<string?> GetBroadcasterTypeAsync(string accessToken, string clientId, string broadcasterId)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.twitch.tv/helix/users?id={Uri.EscapeDataString(broadcasterId)}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("Client-Id", clientId);

                using HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return null;

                string body = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(body);

                if (!doc.RootElement.TryGetProperty("data", out JsonElement dataEl) || dataEl.ValueKind != JsonValueKind.Array || dataEl.GetArrayLength() == 0)
                    return null;

                var userEl = dataEl[0];
                if (!userEl.TryGetProperty("broadcaster_type", out JsonElement typeEl))
                    return null;

                return typeEl.GetString()?.Trim().ToLowerInvariant();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("GetBroadcasterTypeAsync", ex);
                return null;
            }
        }

        private void SetChannelPointsAvailability(string? broadcasterType)
        {
            if (ChannelPointsControlsPanel == null || ChannelPointsAvailabilityText == null)
                return;

            bool eligible = string.Equals(broadcasterType, "affiliate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(broadcasterType, "partner", StringComparison.OrdinalIgnoreCase);

            ChannelPointsControlsPanel.IsEnabled = eligible;
            ChannelPointsControlsPanel.Opacity = eligible ? 1.0 : 0.45;

            ChannelPointsAvailabilityText.Text = eligible
                ? "Channel points are available on this channel (Affiliate/Partner detected)."
                : "Channel points are disabled. Twitch requires Affiliate or Partner status.";
            ChannelPointsAvailabilityText.Foreground = eligible ? Brushes.LimeGreen : Brushes.Orange;
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            await CheckAndApplyChannelPointsAvailabilityAsync(_oauthAccessToken, _settings.TwitchClientId, BroadcasterIdText.Text.Trim());

            if (string.IsNullOrWhiteSpace(ChannelText.Text) ||
                string.IsNullOrWhiteSpace(_oauthAccessToken) ||
                string.IsNullOrWhiteSpace(BroadcasterIdText.Text))
            {
                DialogService.ShowInfo("Please fill in Channel and Broadcaster ID, then click Login with Twitch.");
                return;
            }

            TwitchStatus.Text = "Connecting...";
            TwitchStatus.Foreground = Brushes.Orange;

            try
            {
                var oauthProfile = await GetTwitchProfileAsync(_oauthAccessToken);
                string oauthLogin = oauthProfile?.Login ?? string.Empty;
                if (oauthProfile != null)
                    _oauthUserId = oauthProfile.Value.UserId;

                _twitch.Channel = ChannelText.Text.Trim().ToLower();
                // IRC credentials must use the OAuth account's login name — not a display name
                _twitch.BotName = !string.IsNullOrWhiteSpace(oauthLogin)
                    ? oauthLogin
                    : _twitch.Channel;
                _twitch.OAuth = _oauthAccessToken;
                _twitch.EntryMode = _settings.EntryType;
                _twitch.EntryCommand = _settings.EntryCommand;

                
                

                bool success = await _twitch.ConnectAsync();

                if (!success)
                {
                    TwitchStatus.Text = "IRC Connection failed";
                    TwitchStatus.Foreground = Brushes.Red;
                }
                // Success → OnJoinedChannel handler will connect EventSub and update status
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Connect_Click", ex);
                TwitchStatus.Text = "Error";
                TwitchStatus.Foreground = Brushes.Red;
                DialogService.ShowInfo($"Error: {ex.Message}");
            }
        }

        private async void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            StopActiveUsersPolling();

            // Send goodbye message before disconnecting — read directly from the UI field so any unsaved edits are included
            string botDisplayName = BotNameText.Text.Trim();
            if (string.IsNullOrWhiteSpace(botDisplayName)) botDisplayName = _settings.TwitchBotName;
            try { await _twitch.SendMessageAsync($"{botDisplayName} going offline. Thanks for playing! \U0001F44B"); }
            catch { /* ignore if send fails */ }

            await _twitch.DisconnectAsync();
            await _eventSub.DisconnectAsync();

            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            TwitchStatus.Text = "Disconnected";
            TwitchStatus.Foreground = Brushes.Red;

            Trivia.IsTriviaEnabled = false;
        }
        private void ManualAddEntry_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new InputDialog("Enter username for manual entry:", "");
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Result))
            {
                string user = dlg.Result.Trim();
                if (!Entrants.Contains(user))
                    Entrants.Add(user);
            }
        }

        private void EntrantsContextAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new InputDialog("Enter username to add:", "");
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Result))
            {
                string user = dlg.Result.Trim();
                if (!Entrants.Contains(user))
                    Entrants.Add(user);
            }
        }

        private void EntrantsContextRemove_Click(object sender, RoutedEventArgs e)
        {
            if (EntrantsList.SelectedItem is string selected)
                Entrants.Remove(selected);
        }

        private void EntrantsContextClear_Click(object sender, RoutedEventArgs e)
        {
            if (Entrants.Count == 0) return;
            var result = DialogService.ShowConfirm("Remove all entrants?", "Confirm", yesText: "Yes", noText: "No", cancelText: "Cancel");
            if (result == MessageBoxResult.Yes)
                Entrants.Clear();
        }

        private static BitmapImage? TryCreateBitmapImage(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                return new BitmapImage(new Uri(url));
            }
            catch
            {
                return null;
            }
        }

        private void AddClassicPoolGold_Click(object sender, RoutedEventArgs e)
        {
            var amountDialog = new InputDialog("Gold amount:", "500");
            if (amountDialog.ShowDialog() == true && long.TryParse(amountDialog.Result.Trim(), out long amount) && amount > 0)
            {
                AppendClassicPoolLine($"gold:{amount}");
            }
        }

        private void AddClassicPoolGw2Item_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ItemSelectorDialog();
            if (dialog.ShowDialog() != true || dialog.SelectedItem == null)
                return;

            var amountDialog = new InputDialog("Item amount:", "1");
            long amount = 1;
            if (amountDialog.ShowDialog() == true)
            {
                if (!long.TryParse(amountDialog.Result.Trim(), out amount) || amount <= 0)
                    amount = 1;
            }

            if (dialog.SelectedId <= 0)
                return;

            AppendClassicPoolLine($"item:{dialog.SelectedId}|{amount}");
        }

        private void AddClassicPoolCustom_Click(object sender, RoutedEventArgs e)
        {
            var nameDialog = new InputDialog("Custom prize name:", "Custom Prize");
            if (nameDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(nameDialog.Result))
                return;

            var iconDialog = new InputDialog("Custom icon URL (optional):", "");
            if (iconDialog.ShowDialog() != true)
                return;

            var amountDialog = new InputDialog("Amount:", "1");
            if (amountDialog.ShowDialog() != true)
                return;

            long amount = 1;
            if (!long.TryParse(amountDialog.Result.Trim(), out amount) || amount <= 0)
                amount = 1;

            string name = nameDialog.Result.Trim().Replace("|", " ");
            string icon = iconDialog.Result.Trim().Replace("|", string.Empty);
            AppendClassicPoolLine($"custom:{name}|{icon}|{amount}");
        }

        private void ClearClassicPool_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ClassicPrizePoolText.Text))
                return;

            var result = DialogService.ShowConfirm("Clear all classic pool entries?", "Confirm", yesText: "Yes", noText: "No", cancelText: "Cancel");
            if (result == MessageBoxResult.Yes)
            {
                ClassicPrizePoolText.Text = string.Empty;
            }
        }

        private void AppendClassicPoolLine(string line)
        {
            string existing = ClassicPrizePoolText.Text;
            ClassicPrizePoolText.Text = string.IsNullOrWhiteSpace(existing) ? line : existing.TrimEnd() + Environment.NewLine + line;
            UpdateClassicPoolPreviewUi();
        }

        private void ClassicPrizePoolText_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateClassicPoolPreviewUi();
            if (IsLoaded)
                SaveSettings();
        }

        private void UpdateClassicPoolPreviewUi()
        {
            var poolEntries = ParseClassicPrizePoolEntries(ClassicPrizePoolText.Text);
            if (poolEntries.Count == 0)
            {
                ClassicPoolPreviewText.Text = "Pool preview";
                ClassicPoolPreviewImage.Source = null;
                return;
            }

            var first = poolEntries[0];
            string previewText = first.Amount > 1 ? $"{first.Amount} × {first.Name}" : first.Name;
            if (poolEntries.Count > 1)
                previewText += $" (+{poolEntries.Count - 1} more)";

            ClassicPoolPreviewText.Text = previewText;
            ClassicPoolPreviewImage.Source = TryCreateBitmapImage(first.IconUrl);
        }

        private void ShowOverlay_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            EnsureOverlayReady(showOverlay: false);

            if (_overlay == null)
                return;

            if (_overlay.IsVisible)
                _overlay.Close();
            else
            {
                _overlay.Show();
                _overlay.Activate();
            }

            UpdateOverlayToggleButtons();
        }

        private void EnsureOverlayReady(bool showOverlay)
        {
            if (_overlay == null)
            {
                _overlay = new OverlayWindow();
                ApplySavedWindowBounds(_overlay, _settings.OverlayLeft, _settings.OverlayTop, _settings.OverlayWidth, _settings.OverlayHeight);
                _overlay.IsVisibleChanged += Overlay_IsVisibleChanged;
                _overlay.Closed += (s, e) =>
                {
                    if (TryCaptureWindowBounds(_overlay, out var left, out var top, out var width, out var height))
                    {
                        _settings.OverlayLeft = left;
                        _settings.OverlayTop = top;
                        _settings.OverlayWidth = width;
                        _settings.OverlayHeight = height;
                        PersistSettings();
                    }

                    _overlay = null;
                    UpdateOverlayToggleButtons();
                };
            }

            ConfigureOverlayCallbacks();
            UpdateOverlayClassicPrizePreview();
            _overlay.UpdateEntryInstruction(_settings.EntryCommand);

            if (showOverlay && !_overlay.IsVisible)
            {
                _overlay.Show();
            }

            UpdateOverlayToggleButtons();
        }

        private void Overlay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateOverlayToggleButtons();
        }

        private void TriviaOverlay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateOverlayToggleButtons();
        }

        private void UpdateOverlayToggleButtons()
        {
            if (ShowOverlayButton != null)
                ShowOverlayButton.Content = (_overlay?.IsVisible == true) ? HideOverlayButtonText : ShowOverlayButtonText;

            if (ShowTriviaOverlayButton != null)
                ShowTriviaOverlayButton.Content = (_triviaOverlay?.IsVisible == true) ? HideTriviaOverlayButtonText : ShowTriviaOverlayButtonText;

            if (OpenPrizeBankButton != null)
                OpenPrizeBankButton.Content = (_bankWindow?.IsVisible == true) ? ClosePrizeBankButtonText : OpenPrizeBankButtonText;
        }

        private void UpdateOverlayClassicPrizePreview()
        {
            if (_overlay == null)
                return;

            var mode = _settings.CurrentGiveawayMode;

            // Always update the mode badge
            _overlay.SetGiveawayModeHint(mode);

            if (mode == GiveawayMode.BankOnly)
            {
                // Show bank prizes in a carousel if any slots are filled, else generic bank icon
                var bankPrizes = new List<(BitmapImage? Icon, string Text)>();
                if (Bank?.Slots != null)
                {
                    for (int r = 0; r < Bank.Rows; r++)
                        for (int c = 0; c < Bank.Cols; c++)
                        {
                            var slot = Bank.Slots[r, c];
                            bool hasPrize = slot.Item != null || !string.IsNullOrEmpty(slot.CustomName);
                            if (hasPrize)
                            {
                                string name = slot.CustomName ?? slot.Item?.Name ?? "Prize";
                                string url  = slot.CustomIconUrl ?? slot.Item?.Icon ?? string.Empty;
                                bankPrizes.Add((TryCreateBitmapImage(url), name));
                            }
                        }
                }

                if (bankPrizes.Count > 0)
                {
                    _overlay.UpdatePrize(bankPrizes[0].Text, string.Empty);
                    if (bankPrizes[0].Icon != null)
                        _overlay.SetPrizeImage(bankPrizes[0].Icon!);
                    if (bankPrizes.Count > 1)
                        _overlay.StartPrizePreviewCarousel(bankPrizes);
                }
                else
                {
                    _overlay.UpdatePrize("Bank Prize Roll", "pack://application:,,,/Images/Gold_coin.png");
                }
                return;
            }

            // PrizeOnly or RandomPool — show the configured prize/pool
            var poolEntries = GetClassicPrizePoolEntriesForRoll();
            if (poolEntries.Count > 0)
            {
                var first = poolEntries[0];
                _overlay.UpdatePrize(first.Name, first.IconUrl);

                if (poolEntries.Count > 1)
                {
                    var previewRotation = poolEntries
                        .Select(p => (TryCreateBitmapImage(p.IconUrl), p.Amount > 1 ? $"{p.Amount} × {p.Name}" : p.Name))
                        .ToList();
                    _overlay.StartPrizePreviewCarousel(previewRotation);
                }

                return;
            }

            _overlay.UpdatePrize("Prize", string.Empty);
        }

        private List<ClassicPrizePoolEntry> GetClassicPrizePoolEntriesForRoll()
        {
            var poolEntries = ParseClassicPrizePoolEntries(_settings.ClassicPrizePool);
            if (poolEntries.Count > 0)
                return poolEntries;

            if (_settings.ClassicUsePrizePool && !string.IsNullOrWhiteSpace(_settings.ClassicPrizeName))
            {
                return new List<ClassicPrizePoolEntry>
                {
                    new(_settings.ClassicPrizeName.Trim(), _settings.ClassicPrizeIconUrl ?? string.Empty, 1)
                };
            }

            return poolEntries;
        }

        private void ConfigureOverlayCallbacks()
        {
            if (_overlay == null)
                return;

            _overlay.OnSlotWinnerRevealed = (winnerName, isFromBank) =>
            {
                if (isFromBank)
                {
                    Dispatcher.Invoke(async () => await PerformBankRoll("Bank Roll (overlay)", winnerName));
                }
            };
        }
        private void GenerateTestEntrants_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TestEntrantsCountText.Text, out int count) || count <= 0)
            {
                DialogService.ShowInfo("Enter a valid number > 0");
                return;
            }

            Random rnd = new Random();
            string[] prefixes = { "Viewer", "Gamer", "Twitch", "Stream", "Chat", "Hype", "Legend", "Pro", "Noob", "Boss" };
            string[] suffixes = { "123", "XYZ", "King", "Queen", "Cat", "Dog", "Ninja", "Wizard", "Dragon", "Phoenix" };

            var existing = new HashSet<string>(Entrants, StringComparer.OrdinalIgnoreCase);
            int added = 0;
            int attempts = 0;
            int maxAttempts = Math.Max(count * 20, 2000);

            while (added < count && attempts < maxAttempts)
            {
                string username = prefixes[rnd.Next(prefixes.Length)] + suffixes[rnd.Next(suffixes.Length)] + rnd.Next(1_000_000);
                attempts++;

                if (existing.Add(username))
                {
                    Entrants.Add(username);
                    added++;
                }
            }

            DialogService.ShowInfo($"Added {added} random test entrants!");
        }

        private void StartRoll_Click(object sender, RoutedEventArgs e)
        {
            if (Entrants.Count == 0)
            {
                DialogService.ShowInfo("No entrants yet!");
                return;
            }

            Trivia.IsTriviaPaused = true;
            SaveSettings();
            EnsureOverlayReady(showOverlay: true);

            var mode = _settings.CurrentGiveawayMode;

            // BANK ONLY - show slot wheel for winner reveal, then bank payout via overlay callback
            if (mode == GiveawayMode.BankOnly)
            {
                Random bankRnd = new Random();
                string bankWinner = Entrants[bankRnd.Next(Entrants.Count)];

                // Use a neutral placeholder while winner spins; actual bank prize is chosen in PerformBankRoll callback
                BitmapImage? bankIcon = TryCreateBitmapImage("pack://application:,,,/Images/Gold_coin.png");
                string bankPrizeText = "Bank Prize Roll";

                _overlay?.StartSlotMachine(
                    new System.Collections.Generic.List<string>(Entrants),
                    bankIcon,
                    bankPrizeText,
                    onChatAnnounce: null,
                    forcedWinner: bankWinner,
                    rollDurationSeconds: _settings.SlotRollDurationSeconds,
                    isRandomPoolMode: true,
                    bankPercentage: 100);
                return;
            }

            // PRIZE ONLY or RANDOM POOL - use slot wheel
            // Pick random winner for animation
            Random rnd = new Random();
            int winnerIndex = rnd.Next(Entrants.Count);
            string winner = Entrants[winnerIndex];

            string prizeName;
            string iconUrl;
            long prizeAmount;
            List<ClassicPrizePoolEntry>? selectedPoolEntries = null;
            var poolEntriesForRoll = GetClassicPrizePoolEntriesForRoll();

            if (poolEntriesForRoll.Count > 0)
            {
                selectedPoolEntries = poolEntriesForRoll;
                var selected = poolEntriesForRoll[rnd.Next(poolEntriesForRoll.Count)];
                prizeName = selected.Name;
                prizeAmount = selected.Amount;
                iconUrl = selected.IconUrl;
            }
            else
            {
                prizeName = "Prize";
                prizeAmount = 1;
                iconUrl = string.Empty;
            }

            string prizeText = _settings.WinnerPrizeTemplate
                .Replace("{amount}", prizeAmount.ToString())
                .Replace("{prize}", prizeName);
            BitmapImage? prizeIcon = TryCreateBitmapImage(iconUrl);

            // For random pool mode, pass true to indicate bank vs prize selection
            bool isRandomMode = (mode == GiveawayMode.RandomPool);

            // Call overlay - always show slot wheel for these modes
            _overlay?.ResetToPrize();
            _overlay?.StartSlotMachine(
                new System.Collections.Generic.List<string>(Entrants),
                prizeIcon,
                prizeText,
                selectedWinner =>
                {
                    if (selectedPoolEntries != null && selectedPoolEntries.Count > 1)
                    {
                        var rotation = selectedPoolEntries
                            .Select(p => (TryCreateBitmapImage(p.IconUrl), _settings.WinnerPrizeTemplate.Replace("{amount}", p.Amount.ToString()).Replace("{prize}", p.Name)))
                            .ToList();

                        _overlay?.StartWinnerPrizeCarousel(rotation);

                        string summary = string.Join(", ", selectedPoolEntries.Take(4).Select(p => p.Amount > 1 ? $"{p.Amount} × {p.Name}" : p.Name));
                        if (selectedPoolEntries.Count > 4)
                            summary += ", ...";

                        string chatMsgBundle = $"@{selectedWinner} won a prize bundle: {summary}! Congratulations!";
                        _twitch?.SendMessageAsync(chatMsgBundle);
                        SetCurrentWinner(selectedWinner);

                        string bundlePrize = selectedPoolEntries.Count == 1
                            ? selectedPoolEntries[0].Name
                            : $"Prize Bundle ({selectedPoolEntries.Count} items)";
                        LogGiveawayHistory(selectedWinner, bundlePrize, selectedPoolEntries.Count, "RandomPool:PrizeBundle");
                        return;
                    }

                    string amountPrefix = prizeAmount > 1 ? $"{prizeAmount} × " : string.Empty;
                    string chatMsg = $"@{selectedWinner} won {amountPrefix}{prizeName}! Congratulations!";
                    _twitch?.SendMessageAsync(chatMsg);
                    SetCurrentWinner(selectedWinner);

                    LogGiveawayHistory(selectedWinner, prizeName, prizeAmount, mode == GiveawayMode.PrizeOnly ? "PrizeOnly" : "RandomPool:Prize");
                },
                winner,
                _settings.SlotRollDurationSeconds,
                isRandomMode,
                _settings.RandomPoolBankPercentage
            );
        }

        private async void OpenPrizeBank_Click(object sender, RoutedEventArgs e)
        {
            if (_bankWindow?.IsVisible == true)
            {
                _bankWindow.Close();
                UpdateOverlayToggleButtons();
                return;
            }

            // Ensure item database is loaded (for icons/names in bank)
            if (Gw2ItemDatabase.Items.Count == 0)
            {
                try
                {
                    await Gw2ItemDatabase.LoadAsync(_httpClient);
                }
                catch (Exception ex)
                {
                    DialogService.ShowInfo($"Failed to load item database: {ex.Message}");
                    return;
                }
            }

            // Save settings first to capture the current checkbox state
            SaveSettings();

            if (_bankWindow == null)
            {
                // Load the prize bank from file
                LoadBankFromFile();
                Bank.Hydrate();

                // Create a fresh BankWindow instance
                _bankWindow = new BankWindow(Bank, SaveBank, _settings.ShowPrizeRarityBadges);
                ApplySavedWindowBounds(_bankWindow, _settings.PrizeBankLeft, _settings.PrizeBankTop, _settings.PrizeBankWidth, _settings.PrizeBankHeight);
                _bankWindow.IsVisibleChanged += BankWindow_IsVisibleChanged;

                // Clean up reference when bank window is closed
                _bankWindow.Closed += (s, ev) =>
                {
                    if (TryCaptureWindowBounds(_bankWindow, out var left, out var top, out var width, out var height))
                    {
                        _settings.PrizeBankLeft = left;
                        _settings.PrizeBankTop = top;
                        _settings.PrizeBankWidth = width;
                        _settings.PrizeBankHeight = height;
                        PersistSettings();
                    }

                    _bankWindow = null;
                    UpdateOverlayToggleButtons();
                };
            }

            _bankWindow.Show();
            _bankWindow.Activate();
            UpdateOverlayToggleButtons();
        }

        private void BankWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateOverlayToggleButtons();
        }

        private void ClearEntrants_Click(object sender, RoutedEventArgs e)
        {
            Entrants.Clear();

            // Reset overlay to prize screen
            _overlay?.ResetToPrize();

            // Clear seen users for AllChatters mode (so everyone can enter again next giveaway)
            _twitch?.ClearSeenUsers();
        }

        /// <summary>
        /// Unified bank roll: picks a random filled slot, opens the bank window,
        /// plays the prize-pick animation, consumes the prize, and announces in chat.
        /// When called from the overlay slot wheel, winnerName is the actual slot wheel winner.
        /// When called directly (BankOnly mode), winnerName is null and a random entrant is picked.
        /// </summary>
        private async Task PerformBankRoll(string source, string? winnerName)
        {
            try
            {
                // Ensure bank is loaded
                if (Bank.Slots == null || Bank.Slots.Length == 0)
                {
                    LoadBankFromFile();
                    Bank.Hydrate();
                }

                PrizeWin? win = Bank.GetRandomPrize();
                if (win == null)
                {
                    DialogService.ShowInfo("No prizes or gold in the bank to roll!");
                    return;
                }

                Random rnd = new Random();

                // Open (or reuse) the bank window
                if (_bankWindow == null)
                {
                    if (Gw2ItemDatabase.Items.Count == 0)
                        await Gw2ItemDatabase.LoadAsync(_httpClient);

                    _bankWindow = new BankWindow(Bank, SaveBank, _settings.ShowPrizeRarityBadges);
                    ApplySavedWindowBounds(_bankWindow, _settings.PrizeBankLeft, _settings.PrizeBankTop, _settings.PrizeBankWidth, _settings.PrizeBankHeight);
                    _bankWindow.IsVisibleChanged += BankWindow_IsVisibleChanged;
                    _bankWindow.Closed += (s, ev) =>
                    {
                        if (TryCaptureWindowBounds(_bankWindow, out var left, out var top, out var width, out var height))
                        {
                            _settings.PrizeBankLeft = left;
                            _settings.PrizeBankTop = top;
                            _settings.PrizeBankWidth = width;
                            _settings.PrizeBankHeight = height;
                            PersistSettings();
                        }

                        _bankWindow = null;
                        UpdateOverlayToggleButtons();
                    };
                }

                _bankWindow.Show();
                _bankWindow.Activate();

                // Use the slot wheel winner if provided, otherwise pick a random entrant
                if (string.IsNullOrEmpty(winnerName))
                    winnerName = Entrants.Count > 0 ? Entrants[rnd.Next(Entrants.Count)] : "Winner";

                // 🎬 Play the prize-pick animation, THEN consume
                await _bankWindow.StartRollAsync(win, winnerName, _settings.BankRollDurationSeconds);

                // Consume the prize from the bank
                Bank.ConsumePrize(win);
                SaveBank();

                // Announce to chat
                string prizeName = win.IsGold
                    ? "Gold"
                    : (win.WinItem?.Name ?? win.CustomName ?? "Unknown Prize");
                string amountPrefix = win.IsGold
                    ? string.Empty
                    : (win.WinAmount > 1 ? $"{win.WinAmount} × " : "");
                string chatPrizeText = win.IsGold
                    ? $"{win.WinAmount} Gold"
                    : $"{amountPrefix}{prizeName}";

                await _twitch?.SendMessageAsync($"🏦 @{winnerName} won {chatPrizeText}! Congrats!");
                SetCurrentWinner(winnerName);
                LogGiveawayHistory(winnerName ?? "winner", prizeName, win.WinAmount, source);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("PerformBankRoll", ex);
                DialogService.ShowInfo($"Bank roll error: {ex.Message}");
            }
        }

        private void AddChannelPointReward_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewRewardTitleText.Text))
                return;

            var reward = new ChannelPointReward
            {
                Title = NewRewardTitleText.Text.Trim(),
                Cost = int.TryParse(NewRewardCostText.Text, out int cost) ? cost : 100,
                Action = (ChannelPointAction)NewRewardActionCombo.SelectedIndex,
                InstantGoldAmount = long.TryParse(NewRewardGoldText.Text, out long gold) ? gold : 0
            };

            _channelPointRewards.Add(reward);

            // Clear fields for next
            NewRewardTitleText.Text = "New Reward";
            NewRewardCostText.Text = "100";
            NewRewardActionCombo.SelectedIndex = 0;
            NewRewardGoldText.Text = "0";

            SaveSettings();
        }

        private void ManualRedeem_Click(object sender, RoutedEventArgs e)
        {
            if (RedeemRewardCombo.SelectedItem is not ChannelPointReward reward)
            {
                DialogService.ShowInfo("Select a reward");
                return;
            }

            string username = RedeemUsernameText.Text.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                DialogService.ShowInfo("Enter username");
                return;
            }

            switch (reward.Action)
            {
                case ChannelPointAction.AddToEntrants:
                    if (!Entrants.Contains(username))
                        Entrants.Add(username);
                    _twitch?.SendMessageAsync($"@{username} redeemed {reward.Title} and joined the giveaway!");
                    break;

                case ChannelPointAction.InstantBankRoll:
                    InstantBankRollForUser(username);
                    break;

                case ChannelPointAction.InstantGoldWin:
                    if (reward.InstantGoldAmount > 0)
                    {
                        // You could add gold to a user database if you have one, or just announce
                        _twitch?.SendMessageAsync($"@{username} redeemed {reward.Title} and won {reward.InstantGoldAmount} Gold!");
                    }
                    break;
            }

            RedeemUsernameText.Text = "";
        }
        public void AddEntrant(string username)
        {
            Dispatcher.Invoke(() =>
            {
                username = username.ToLowerInvariant();
                if (!Entrants.Contains(username))
                    Entrants.Add(username);
            });
        }
        public void InstantBankRollForUser(string username)
        {
            Dispatcher.Invoke(() =>
            {
                PrizeWin? win = Bank.GetRandomPrize();
                if (win == null)
                {
                    _twitch?.SendMessageAsync($"@{username} redeemed channel point but no prizes left!");
                    return;
                }

                Bank.ConsumePrize(win);
                SaveBank();

                long amount = win.WinAmount;
                string prizeName = win.IsGold ? "Gold" : (win.CustomName ?? win.WinItem?.Name ?? "Prize");
                string prizeText = _settings.WinnerPrizeTemplate.Replace("{amount}", amount.ToString()).Replace("{prize}", prizeName);

                string iconUrl = win.IsGold ? "pack://application:,,,/Images/Gold_coin.png" : (win.CustomIconUrl ?? win.WinItem?.Icon);
                BitmapImage? prizeIcon = TryCreateBitmapImage(iconUrl);

                _overlay?.ShowBankWinner(username, prizeIcon, prizeText);

                string chatPrize = amount > 1 ? $"{amount} × {prizeName}" : prizeName;
                if (prizeName == "Gold") chatPrize = $"{amount} Gold";

                _twitch?.SendMessageAsync($"@{username} redeemed channel point and instantly won {chatPrize}! Congrats!");
                LogGiveawayHistory(username, prizeName, amount, "ChannelPoint:InstantBankRoll");
            });
        }
        private void RemoveChannelPointReward_Click(object sender, RoutedEventArgs e)
        {
            if (ChannelPointRewardsList.SelectedItem is ChannelPointReward selectedReward)
            {
                _channelPointRewards.Remove(selectedReward);
                SaveSettings(); // persist the removal
            }
            else
            {
                DialogService.ShowInfo("Select a reward from the list to remove it.");
            }
        }

        private void EntryModeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateEntryCommandFieldState();
            if (IsLoaded)
                SaveSettings();
        }

        private void FollowersOnlyCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) SaveSettings();
        }

        private void SubBonusEntriesText_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (IsLoaded) SaveSettings();
        }

        private void WinnerMessagesList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (WinnerMessagesList.SelectedItem is WinnerChatMessage msg)
            {
                try { System.Windows.Clipboard.SetText(msg.ToString()); }
                catch { /* clipboard unavailable */ }
            }
        }

        private void UpdateEntryCommandFieldState()
        {
            if (EntryModeCombo == null || EntryCommandText == null)
                return;

            bool commandMode = EntryModeCombo.SelectedIndex == (int)EntryMode.Command;
            bool gw2Mode = EntryModeCombo.SelectedIndex == (int)EntryMode.Gw2Account;
            EntryCommandText.IsEnabled = commandMode;
            EntryCommandText.Opacity = commandMode ? 1.0 : 0.6;
            if (gw2Mode)
                EntryCommandText.ToolTip = "GW2 mode: viewers type their GW2 account name (e.g. PlayerName.1234) in chat to enter.";
            else
                EntryCommandText.ToolTip = "Command text (e.g. !enter)";
        }

        private void StartEntries_Click(object sender, RoutedEventArgs e)
        {
            if (_entriesOpen)
            {
                DialogService.ShowInfo("Entries already open!");
                return;
            }
            Trivia.IsTriviaPaused = true;
            if (!int.TryParse(EntryTimeText.Text, out _entryTimeSeconds) || _entryTimeSeconds < 0)
                _entryTimeSeconds = 0;

            _entriesOpen = true;
            // Keep giveaway history across rolls; only reset current entrant list for the new period
            Entrants.Clear();
            _gw2AccountMap.Clear();
            _followerCache.Clear();
            SetCurrentWinner(null);

            if (_settings.EntryType == EntryMode.ActiveUsers)
            {
                _ = ImportCurrentChattersAsync(announceErrors: true);
                StartActiveUsersPolling();
            }

            string followersNote = _settings.FollowersOnly ? " (Followers only)" : string.Empty;
            string subNote = _settings.SubscriberBonusEntries > 1 ? $" Subs get {_settings.SubscriberBonusEntries}x entries!" : string.Empty;
            string openMessage = _settings.EntryType == EntryMode.ActiveUsers
                ? $"Giveaway entries OPEN! Active Users mode collecting chatters.{followersNote}{subNote}"
                : _settings.EntryType == EntryMode.Gw2Account
                    ? $"Giveaway entries OPEN! Type your GW2 account name (e.g. PlayerName.1234) to enter!{followersNote}{subNote}"
                    : $"Giveaway entries OPEN{(_entryTimeSeconds > 0 ? $" for {TimeSpan.FromSeconds(_entryTimeSeconds):mm\\:ss}" : " (unlimited)")}! Type {_settings.EntryCommand} to join!{followersNote}{subNote}";
            _twitch?.SendMessageAsync(openMessage);

            EntryStatusText.Text = _entryTimeSeconds > 0 ? $"Entries Open – {_entryTimeSeconds}s remaining" : "Entries Open (unlimited)";
            EntryStatusText.Foreground = Brushes.LimeGreen;

            // Start overlay countdown if overlay is open
            EnsureOverlayReady(showOverlay: false);

            // Set the instruction line to match the current entry mode
            if (_overlay != null)
            {
                string instruction = _settings.EntryType switch
                {
                    EntryMode.ActiveUsers      => "Active chatters are entered automatically!",
                    EntryMode.Gw2Account       => "Type your GW2 account name (e.g. Name.1234) to enter",
                    EntryMode.ChannelPointManual => "Redeem channel points to enter!",
                    _                          => $"Type {_settings.EntryCommand} in chat to join"
                };
                _overlay.SetEntryInstruction(instruction);
            }

            _overlay?.StartCountdown(_entryTimeSeconds, Entrants.Count);

            if (_entryTimeSeconds > 0)
            {
                _entryTimer = new DispatcherTimer();
                _entryTimer.Interval = TimeSpan.FromSeconds(1);
                _entryTimer.Tick += EntryTimer_Tick;
                _entryTimer.Start();
            }
        }

        private void StopEntries_Click(object sender, RoutedEventArgs e)
        {
            if (!_entriesOpen)
            {
                DialogService.ShowInfo("Entries not open!");
                return;
            }

            _entriesOpen = false;
            _entryTimer?.Stop();
            StopActiveUsersPolling();
            _overlay?.StopCountdown();

            _twitch?.SendMessageAsync($"Giveaway entries CLOSED! Total entrants: {Entrants.Count}");

            EntryStatusText.Text = "Entries: Closed";
            EntryStatusText.Foreground = Brushes.Red;
            Trivia.IsTriviaPaused = false;
        }

        private void EntryTimer_Tick(object? sender, EventArgs e)
        {
            _entryTimeSeconds--;
            if (_entryTimeSeconds <= 0)
            {
                StopEntries_Click(null, null); // auto-close
                Trivia.IsTriviaPaused = false;
                return;
            }

            EntryStatusText.Text = $"Entries Open – {_entryTimeSeconds}s remaining";
        }

        // Updated Twitch_OnMessageReceived – handles all entry modes, follower check, sub bonus, winner messages
        private void Twitch_OnMessageReceived(string username, string message, bool isSubscriber)
        {
            username = username.ToLowerInvariant();

            // Always track messages from the current winner for the Winners tab
            if (!string.IsNullOrEmpty(_currentWinner) &&
                string.Equals(username, _currentWinner, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() =>
                {
                    WinnerMessages.Add(new WinnerChatMessage { Sender = username, Text = message });
                    // Auto-scroll
                    if (WinnerMessagesList.Items.Count > 0)
                        WinnerMessagesList.ScrollIntoView(WinnerMessagesList.Items[^1]);
                });
            }

            if (!_entriesOpen) return;

            // Fire-and-forget async entry processing to keep the event handler non-blocking
            _ = ProcessEntryAsync(username, message, isSubscriber);
        }

        private async Task ProcessEntryAsync(string username, string message, bool isSubscriber)
        {
            // Follower-only check (with cache)
            if (_settings.FollowersOnly)
            {
                bool isFollower = await IsFollowerCachedAsync(username);
                if (!isFollower) return;
            }

            int entryCount = 1;
            if (_settings.SubscriberBonusEntries > 1 && isSubscriber)
                entryCount = _settings.SubscriberBonusEntries;

            await Dispatcher.InvokeAsync(() =>
            {
                if (_settings.EntryType == EntryMode.Command)
                {
                    if (message.Equals(_settings.EntryCommand, StringComparison.OrdinalIgnoreCase))
                        AddEntrantWithBonus(username, entryCount);
                }
                else if (_settings.EntryType == EntryMode.ActiveUsers)
                {
                    AddEntrantWithBonus(username, entryCount);
                }
                else if (_settings.EntryType == EntryMode.Gw2Account)
                {
                    // Expect message to contain the GW2 account name, e.g. "PlayerName.1234"
                    // Simple validation: must contain a dot followed by 4 digits
                    string trimmed = message.Trim();
                    if (IsValidGw2AccountName(trimmed))
                    {
                        _gw2AccountMap[username] = trimmed;
                        AddEntrantWithBonus(username, entryCount);
                        // Refresh display so GW2 name shows up alongside Twitch name
                        int idx = Entrants.IndexOf(username);
                        if (idx >= 0)
                        {
                            // Force ListBox refresh by removing and re-inserting at same index
                            Entrants.RemoveAt(idx);
                            Entrants.Insert(idx, username);
                        }
                    }
                }
            });
        }

        private static bool IsValidGw2AccountName(string name)
        {
            // GW2 account names look like "DisplayName.NNNN" (4 digits at the end)
            if (string.IsNullOrWhiteSpace(name)) return false;
            int dot = name.LastIndexOf('.');
            if (dot < 1 || dot >= name.Length - 1) return false;
            string suffix = name[(dot + 1)..];
            return suffix.Length == 4 && suffix.All(char.IsDigit);
        }

        private void AddEntrantWithBonus(string username, int entryCount)
        {
            if (entryCount <= 1)
            {
                if (!Entrants.Contains(username))
                {
                    Entrants.Add(username);
                    _overlay?.UpdateEntrantCount(Entrants.Distinct().Count());
                }
                return;
            }

            // Subscriber bonus: add username multiple times (displayed as "username", "username [2]", etc.)
            if (!Entrants.Contains(username))
                Entrants.Add(username);

            for (int i = 2; i <= entryCount; i++)
            {
                // Add duplicate entries so random pick naturally weights them
                Entrants.Add(username);
            }

            _overlay?.UpdateEntrantCount(Entrants.Distinct().Count());
        }

        /// <summary>Check follower status with a 5-minute in-memory cache.</summary>
        private async Task<bool> IsFollowerCachedAsync(string username)
        {
            if (_followerCache.TryGetValue(username, out var cached) && DateTime.UtcNow < cached.Expiry)
                return cached.IsFollower;

            bool isFollower = await CheckIsFollowerAsync(username);
            _followerCache[username] = (isFollower, DateTime.UtcNow + FollowerCacheTtl);
            return isFollower;
        }

        private async Task<bool> CheckIsFollowerAsync(string username)
        {
            try
            {
                string broadcasterId = BroadcasterIdText.Text.Trim();
                string moderatorId = _oauthUserId?.Trim() ?? string.Empty;
                string clientId = _settings.TwitchClientId;
                string token = _oauthAccessToken;

                if (string.IsNullOrWhiteSpace(broadcasterId) || string.IsNullOrWhiteSpace(moderatorId) ||
                    string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(token))
                    return true; // Fail open if not configured

                string normalizedToken = token.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase)
                    ? token["oauth:".Length..]
                    : token;

                // Look up user ID by login first
                string userId = await GetUserIdByLoginAsync(username, normalizedToken, clientId);
                if (string.IsNullOrEmpty(userId)) return false;

                string url = $"https://api.twitch.tv/helix/channels/followers?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&user_id={Uri.EscapeDataString(userId)}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", normalizedToken);
                request.Headers.Add("Client-Id", clientId);

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return true; // Fail open on API error

                string body = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("total", out var totalEl) && totalEl.GetInt32() > 0)
                    return true;
                return false;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("CheckIsFollowerAsync", ex);
                return true; // Fail open
            }
        }

        private async Task<string> GetUserIdByLoginAsync(string login, string token, string clientId)
        {
            try
            {
                string url = $"https://api.twitch.tv/helix/users?login={Uri.EscapeDataString(login)}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("Client-Id", clientId);
                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return string.Empty;
                string body = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                    return data[0].TryGetProperty("id", out var id) ? (id.GetString() ?? string.Empty) : string.Empty;
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        /// <summary>Sets the current winner so their chat messages populate the Winners tab.</summary>
        private void SetCurrentWinner(string? winnerName)
        {
            _currentWinner = winnerName?.ToLowerInvariant();
            Dispatcher.Invoke(() =>
            {
                WinnerMessages.Clear();
                if (!string.IsNullOrEmpty(_currentWinner))
                    WinnerMessages.Add(new WinnerChatMessage { Sender = "System", Text = $"Now tracking messages from @{_currentWinner}" });
            });
        }

        private void GiveawayModeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return; // Avoid firing during initialization
            var mode = (GiveawayMode)GiveawayModeCombo.SelectedIndex;
            RandomPoolPanel.Visibility = (mode == GiveawayMode.RandomPool) ? Visibility.Visible : Visibility.Collapsed;
            SaveSettings();

            // Refresh overlay preview to match the newly selected mode
            if (_overlay != null)
                UpdateOverlayClassicPrizePreview();
        }

        private void BankPercentageSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateBankPercentageDisplay();
            if (IsLoaded) SaveSettings();
        }

        private void UpdateBankPercentageDisplay()
        {
            if (BankPercentageSlider == null || BankPercentageText == null || PrizePercentageText == null)
                return;

            int bankPct = (int)BankPercentageSlider.Value;
            int prizePct = 100 - bankPct;
            BankPercentageText.Text = $"{bankPct}%";
            PrizePercentageText.Text = $"{prizePct}%";
        }

        private void SlotRollDurationSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSlotRollDurationDisplay();
            if (IsLoaded) SaveSettings();
        }

        private void UpdateSlotRollDurationDisplay()
        {
            if (SlotRollDurationDisplay != null)
                SlotRollDurationDisplay.Text = $"{(int)SlotRollDurationSlider.Value}s";
        }

        private void BankRollDurationSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateBankRollDurationDisplay();
            if (IsLoaded) SaveSettings();
        }

        private void UpdateBankRollDurationDisplay()
        {
            if (BankRollDurationDisplay != null)
                BankRollDurationDisplay.Text = $"{(int)BankRollDurationSlider.Value}s";
        }

        private List<ClassicPrizePoolEntry> ParseClassicPrizePoolEntries(string? raw)
        {
            var entries = new List<ClassicPrizePoolEntry>();
            if (string.IsNullOrWhiteSpace(raw))
                return entries;

            foreach (string lineRaw in raw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = lineRaw.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("gold:", StringComparison.OrdinalIgnoreCase))
                {
                    string amountPart = line["gold:".Length..].Trim();
                    if (long.TryParse(amountPart, out long gold) && gold > 0)
                    {
                        entries.Add(new ClassicPrizePoolEntry("Gold", "pack://application:,,,/Images/Gold_coin.png", gold));
                    }
                    continue;
                }

                if (line.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
                {
                    string payload = line["item:".Length..].Trim();
                    string[] parts = payload.Split('|');
                    if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), out int itemId) && Gw2ItemDatabase.Items.TryGetValue(itemId, out var item))
                    {
                        long amount = 1;
                        if (parts.Length >= 2)
                            long.TryParse(parts[1].Trim(), out amount);
                        if (amount <= 0) amount = 1;

                        entries.Add(new ClassicPrizePoolEntry(item.Name, item.Icon ?? string.Empty, amount));
                    }
                    continue;
                }

                if (line.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
                {
                    string payload = line["custom:".Length..].Trim();
                    string[] parts = payload.Split('|');
                    string name = parts.Length >= 1 ? parts[0].Trim() : "Custom Prize";
                    string icon = parts.Length >= 2 ? parts[1].Trim() : string.Empty;
                    long amount = 1;
                    if (parts.Length >= 3)
                        long.TryParse(parts[2].Trim(), out amount);
                    if (amount <= 0) amount = 1;
                    if (string.IsNullOrWhiteSpace(name)) name = "Custom Prize";

                    entries.Add(new ClassicPrizePoolEntry(name, icon, amount));
                    continue;
                }

                // fallback format: Name|IconUrl|Amount
                string[] fallback = line.Split('|');
                if (fallback.Length >= 1)
                {
                    string name = fallback[0].Trim();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    string icon = fallback.Length >= 2 ? fallback[1].Trim() : string.Empty;
                    long amount = 1;
                    if (fallback.Length >= 3)
                        long.TryParse(fallback[2].Trim(), out amount);
                    if (amount <= 0) amount = 1;

                    entries.Add(new ClassicPrizePoolEntry(name, icon, amount));
                }
            }

            return entries;
        }

        private async Task RefreshDataManagementViewAsync(string? triviaQuery = null, string? historyQuery = null)
        {
            if (DataViewersList == null || DataStatusText == null)
                return;

            try
            {
                var rows = await _databaseService.SearchViewersAsync(triviaQuery, 500);
                DataViewersList.ItemsSource = rows.Select(r => new TriviaViewerRow
                {
                    Username = r.Username,
                    Iq = r.Iq
                }).ToList();

                var historyRows = await _databaseService.GetWinnersAsync(historyQuery, 500);

                if (DataHistoryList != null)
                {
                    DataHistoryList.ItemsSource = historyRows
                        .Take(300)
                        .Select(h => new
                        {
                            Date   = h.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                            Winner = h.Winner,
                            Prize  = h.Amount > 1 ? $"{h.Amount} × {h.Prize}" : h.Prize,
                            Source = h.Source
                        })
                        .ToList();
                }

                DataStatusText.Text = $"Trivia: {rows.Count} records • Winners: {historyRows.Count} shown";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MainWindow.RefreshDataManagementViewAsync", ex);
                DataStatusText.Text = "Failed to load data";
            }
        }

        private async void DataTriviaSearch_Click(object sender, RoutedEventArgs e)
        {
            string triviaQuery = DataTriviaSearchText?.Text?.Trim() ?? string.Empty;
            string historyQuery = DataHistorySearchText?.Text?.Trim() ?? string.Empty;
            await RefreshDataManagementViewAsync(triviaQuery, historyQuery);
        }

        private async void DataTriviaRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (DataTriviaSearchText != null)
                DataTriviaSearchText.Text = string.Empty;

            await RefreshDataManagementViewAsync(null, DataHistorySearchText?.Text?.Trim());
        }

        private async void DataHistorySearch_Click(object sender, RoutedEventArgs e)
        {
            string triviaQuery = DataTriviaSearchText?.Text?.Trim() ?? string.Empty;
            string historyQuery = DataHistorySearchText?.Text?.Trim() ?? string.Empty;
            await RefreshDataManagementViewAsync(triviaQuery, historyQuery);
        }

        private async void DataHistoryRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (DataHistorySearchText != null)
                DataHistorySearchText.Text = string.Empty;

            await RefreshDataManagementViewAsync(DataTriviaSearchText?.Text?.Trim(), null);
        }

        private async void DataClearTrivia_Click(object sender, RoutedEventArgs e)
        {
            var result = DialogService.ShowConfirm(
                "Clear all saved Trivia IQ records? This cannot be undone.",
                "Clear Trivia Data",
                yesText: "Clear",
                noText: "Cancel",
                cancelText: "Cancel");

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                await _databaseService.ClearAllViewersAsync();
                await RefreshDataManagementViewAsync(DataTriviaSearchText?.Text?.Trim(), DataHistorySearchText?.Text?.Trim());
                DialogService.ShowInfo("All Trivia IQ data has been cleared.");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MainWindow.DataClearTrivia_Click", ex);
                DialogService.ShowInfo("Failed to clear trivia data: " + ex.Message);
            }
        }

        private async void DataClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = DialogService.ShowConfirm(
                "Clear all giveaway winner history? This cannot be undone.",
                "Clear Winner History",
                yesText: "Clear",
                noText: "Cancel",
                cancelText: "Cancel");

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                await _databaseService.ClearAllWinnersAsync();
                await RefreshDataManagementViewAsync(DataTriviaSearchText?.Text?.Trim(), DataHistorySearchText?.Text?.Trim());
                DialogService.ShowInfo("Giveaway winner history has been cleared.");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MainWindow.DataClearHistory_Click", ex);
                DialogService.ShowInfo("Failed to clear winner history: " + ex.Message);
            }
        }

        private void ExportAppBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettings();
                SaveBank();

                var dialog = new SaveFileDialog
                {
                    Title = "Export Gw2Giveaway Backup",
                    Filter = "Zip files (*.zip)|*.zip",
                    FileName = $"Gw2Giveaway-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
                    AddExtension = true,
                    DefaultExt = ".zip"
                };

                if (dialog.ShowDialog() != true)
                    return;

                string tempRoot = Path.Combine(Path.GetTempPath(), "Gw2GiveawayBackup", Guid.NewGuid().ToString("N"));
                string tempData = Path.Combine(tempRoot, "Data");
                Directory.CreateDirectory(tempData);

                if (Directory.Exists(DataFolder))
                {
                    foreach (string file in Directory.GetFiles(DataFolder, "*", SearchOption.AllDirectories))
                    {
                        string relative = Path.GetRelativePath(DataFolder, file);
                        string target = Path.Combine(tempData, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        File.Copy(file, target, overwrite: true);
                    }
                }

                if (File.Exists(ViewersDbFile))
                {
                    File.Copy(ViewersDbFile, Path.Combine(tempRoot, "viewers.db"), overwrite: true);
                }

                if (File.Exists(dialog.FileName))
                    File.Delete(dialog.FileName);

                ZipFile.CreateFromDirectory(tempRoot, dialog.FileName, CompressionLevel.Optimal, includeBaseDirectory: false);
                Directory.Delete(tempRoot, recursive: true);

                DialogService.ShowInfo("Backup exported successfully.");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MainWindow.ExportAppBackup_Click", ex);
                DialogService.ShowInfo("Backup export failed: " + ex.Message);
            }
        }

        private async void ImportAppBackup_Click(object sender, RoutedEventArgs e)
        {
            var confirm = DialogService.ShowConfirm(
                "Importing backup will overwrite current settings, trivia data, and bank data. Continue?",
                "Import Backup",
                yesText: "Import",
                noText: "Cancel",
                cancelText: "Cancel");

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Import Gw2Giveaway Backup",
                    Filter = "Zip files (*.zip)|*.zip",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (dialog.ShowDialog() != true)
                    return;

                _overlay?.Close();
                _triviaOverlay?.Close();
                _bankWindow?.Close();

                string extractRoot = Path.Combine(Path.GetTempPath(), "Gw2GiveawayBackupImport", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(extractRoot);
                ZipFile.ExtractToDirectory(dialog.FileName, extractRoot);

                string extractedData = Path.Combine(extractRoot, "Data");
                if (Directory.Exists(extractedData))
                {
                    Directory.CreateDirectory(DataFolder);
                    foreach (string file in Directory.GetFiles(extractedData, "*", SearchOption.AllDirectories))
                    {
                        string relative = Path.GetRelativePath(extractedData, file);
                        string target = Path.Combine(DataFolder, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        File.Copy(file, target, overwrite: true);
                    }
                }

                string extractedDb = Path.Combine(extractRoot, "viewers.db");
                if (File.Exists(extractedDb))
                {
                    File.Copy(extractedDb, ViewersDbFile, overwrite: true);
                }

                Directory.Delete(extractRoot, recursive: true);

                LoadSettings();
                LoadBankFromFile();
                await RefreshDataManagementViewAsync();
                UpdateOverlayToggleButtons();

                DialogService.ShowInfo("Backup imported successfully.");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MainWindow.ImportAppBackup_Click", ex);
                DialogService.ShowInfo("Backup import failed: " + ex.Message);
            }
        }

        private void LogGiveawayHistory(string winner, string prize, long amount, string source)
        {
            try
            {
                var entry = new GiveawayHistoryEntry
                {
                    TimestampUtc = DateTime.UtcNow,
                    Winner = winner?.Trim() ?? string.Empty,
                    Prize  = prize?.Trim()  ?? string.Empty,
                    Amount = amount <= 0 ? 1 : amount,
                    Source = source?.Trim() ?? string.Empty
                };

                // Persist to DB (fire-and-forget; in-memory list is refreshed after)
                _ = _databaseService.AddWinnerAsync(entry);

                _ = RefreshDataManagementViewAsync(DataTriviaSearchText?.Text?.Trim(), DataHistorySearchText?.Text?.Trim());
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MainWindow.LogGiveawayHistory", ex);
            }
        }

        private readonly record struct TwitchOAuthToken(string AccessToken, string RefreshToken);
        private readonly record struct ClassicPrizePoolEntry(string Name, string IconUrl, long Amount);


    }
}
