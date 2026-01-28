using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Gw2Giveaway
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<string> Entrants { get; } = new();
        private TwitchChat? _twitch;
        private OverlayWindow? _overlay;
        private BankWindow? _bankWindow;
        private bool _entriesOpen = false;
        private DispatcherTimer _entryTimer;
        private int _entryTimeSeconds = 300;
        public PrizeBank Bank { get; private set; } = new();
        private readonly HttpClient _httpClient = new HttpClient();
        private static readonly string DataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        private static readonly string BankFile = Path.Combine(DataFolder, "prizebank.json");
        private static readonly string SettingsFile = Path.Combine(DataFolder, "settings.json");
        private AppSettings _settings = new();
        private ObservableCollection<ChannelPointReward> _channelPointRewards = new();
        public MainWindow()
        {
            InitializeComponent();
            EntrantsList.ItemsSource = Entrants;
            LoadSettings();
            LoadDatabaseWithProgress();
        }

        private void LoadSettings()
        {
            if (File.Exists(SettingsFile))
            {
                try
                {
                    string json = File.ReadAllText(SettingsFile);
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                catch { /* corrupted – use defaults */ }
            }

            ChannelText.Text = _settings.TwitchChannel;
            BotNameText.Text = _settings.TwitchBotName;
            OAuthBox.Password = _settings.TwitchOAuth;
            EntryModeCombo.SelectedIndex = (int)_settings.EntryType;
            EntryCommandText.Text = _settings.EntryCommand;
            ChatTemplateText.Text = _settings.ChatTemplate;
            WinnerPrizeTemplateText.Text = _settings.WinnerPrizeTemplate;
            RollModeCombo.SelectedIndex = (int)_settings.CurrentRollMode;
            ShowRarityBadgesCheck.IsChecked = _settings.ShowPrizeRarityBadges;
            ClassicPrizeNameText.Text = _settings.ClassicPrizeName;
            ClassicPrizeInputText.Text = _settings.ClassicPrizeIconUrl;
            LoadClassicPreview(_settings.ClassicPrizeIconUrl);
            _channelPointRewards = _settings.ChannelPointRewards;
        }

        private void SaveSettings()
        {
            _settings.TwitchChannel = ChannelText.Text;
            _settings.TwitchBotName = BotNameText.Text;
            _settings.TwitchOAuth = OAuthBox.Password;
            _settings.EntryType = (EntryMode)EntryModeCombo.SelectedIndex;
            _settings.EntryCommand = EntryCommandText.Text;
            _settings.ChatTemplate = ChatTemplateText.Text;
            _settings.WinnerPrizeTemplate = WinnerPrizeTemplateText.Text;
            _settings.CurrentRollMode = (RollMode)RollModeCombo.SelectedIndex;
            _settings.ShowPrizeRarityBadges = ShowRarityBadgesCheck.IsChecked == true;
            _settings.ClassicPrizeName = ClassicPrizeNameText.Text;
            _settings.ClassicPrizeIconUrl = ClassicPrizeInputText.Text;
            _settings.ChannelPointRewards = _channelPointRewards;
            _overlay?.UpdateEntryInstruction(EntryCommandText.Text);
            try
            {
                string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                Directory.CreateDirectory(DataFolder);
                File.WriteAllText(SettingsFile, json);
            }
            catch { /* ignore */ }
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
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    DatabaseProgress.Value = 0;
                    DatabaseStatus.Text = "Load failed";
                    MessageBox.Show("Database error: " + ex.Message + "\nCheck internet connection.");
                });
            }
        }

        private async void RefreshDatabase_Click(object sender, RoutedEventArgs e)
        {
            await LoadDatabaseWithProgress(true);
        }

        private void LoadBankFromFile()
        {
            if (!File.Exists(BankFile))
            {
                Bank = new PrizeBank();
                SaveBank();
                return;
            }

            try
            {
                string json = File.ReadAllText(BankFile);
                var loaded = JsonConvert.DeserializeObject<PrizeBank>(json);
                if (loaded != null)
                {
                    Bank = loaded;

                    if (Bank.Slots == null || Bank.Slots.GetLength(0) != 3 || Bank.Slots.GetLength(1) != 10)
                    {
                        Bank = new PrizeBank();
                    }
                }
                else
                {
                    Bank = new PrizeBank();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load prize bank – starting with fresh empty bank.\n" + ex.Message);
                Bank = new PrizeBank();
            }

            Bank.Hydrate();
            SaveBank();
        }

        private void SaveBank()
        {
            try
            {
                Directory.CreateDirectory(DataFolder);
                string json = JsonConvert.SerializeObject(Bank, Formatting.Indented);
                File.WriteAllText(BankFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save failed: " + ex.Message + "\nPath: " + BankFile);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            SaveBank();
            base.OnClosing(e);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            if (string.IsNullOrWhiteSpace(ChannelText.Text) || string.IsNullOrWhiteSpace(OAuthBox.Password))
            {
                MessageBox.Show("Please fill in Channel and OAuth.");
                return;
            }

            TwitchStatus.Text = "Connecting...";
            TwitchStatus.Foreground = Brushes.Orange;

            try
            {
                _twitch = new TwitchChat
                {
                    Channel = ChannelText.Text.Trim().ToLower(),
                    BotName = BotNameText.Text.Trim().ToLower(),
                    OAuth = "oauth:" + OAuthBox.Password.Trim(),
                    EntryMode = _settings.EntryType,
                    EntryCommand = _settings.EntryCommand
                };

                _twitch.OnConnected += () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TwitchStatus.Text = "Connected & Joined!";
                        TwitchStatus.Foreground = Brushes.LimeGreen;
                        ConnectButton.IsEnabled = false;
                        DisconnectButton.IsEnabled = true;
                    });
                };

                _twitch.OnJoinedChannel += () =>
                {
                    // Already handled in OnConnected for simplicity
                };

                _twitch.OnConnectionError += error =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TwitchStatus.Text = "Connection failed";
                        TwitchStatus.Foreground = Brushes.Red;
                        MessageBox.Show($"Twitch connection failed: {error}");
                        ConnectButton.IsEnabled = true;
                        DisconnectButton.IsEnabled = false;
                    });
                };

                // NEW: Subscribe to raw messages (for entry period control)
                _twitch.OnMessageReceived += Twitch_OnMessageReceived;

                // REMOVED NewEntrant subscription – we handle entrants manually now

                _twitch.Connect();
            }
            catch (Exception ex)
            {
                TwitchStatus.Text = "Error";
                TwitchStatus.Foreground = Brushes.Red;
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            _twitch?.Disconnect();

            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            TwitchStatus.Text = "Disconnected";
            TwitchStatus.Foreground = Brushes.Red;
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

        private void ClassicPrizeFetch_Click(object sender, RoutedEventArgs e)
        {
            string input = ClassicPrizeInputText.Text.Trim();
            if (int.TryParse(input, out int id))
            {
                FetchClassicItem(id);
            }
            else if (Uri.TryCreate(input, UriKind.Absolute, out _))
            {
                LoadClassicPreview(input);
                ClassicPrizeNameText.Text = "Custom Prize";
            }
        }

        private async void FetchClassicItem(int id)
        {
            try
            {
                string json = await _httpClient.GetStringAsync($"https://api.guildwars2.com/v2/items/{id}");
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                string name = root.GetProperty("name").GetString()!;
                string icon = root.GetProperty("icon").GetString()!;
                ClassicPrizeNameText.Text = name;
                ClassicPrizeInputText.Text = icon;
                LoadClassicPreview(icon);
            }
            catch
            {
                MessageBox.Show("Item not found");
            }
        }

        private void LoadClassicPreview(string url)
        {
            if (!string.IsNullOrEmpty(url))
                ClassicPrizePreview.Source = new BitmapImage(new Uri(url));
            else
                ClassicPrizePreview.Source = null;

            _overlay?.UpdatePrize(ClassicPrizeNameText.Text, url);
        }

        private void SelectClassicPrizeItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ItemSelectorDialog();
            if (dialog.ShowDialog() == true && dialog.SelectedItem != null)
            {
                ClassicPrizeNameText.Text = dialog.SelectedItem.Name;
                ClassicPrizeInputText.Text = dialog.SelectedItem.Icon;
                LoadClassicPreview(dialog.SelectedItem.Icon);
            }
        }

        private void ShowOverlay_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            _overlay ??= new OverlayWindow();
            _overlay.UpdatePrize(ClassicPrizeNameText.Text, ClassicPrizeInputText.Text);

            // NEW: Update the entry command text in overlay
            _overlay.UpdateEntryInstruction(_settings.EntryCommand);

            _overlay.Show();
            _overlay.Activate();
        }
        private void GenerateTestEntrants_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TestEntrantsCountText.Text, out int count) || count <= 0)
            {
                MessageBox.Show("Enter a valid number > 0");
                return;
            }

            Random rnd = new Random();
            string[] prefixes = { "Viewer", "Gamer", "Twitch", "Stream", "Chat", "Hype", "Legend", "Pro", "Noob", "Boss" };
            string[] suffixes = { "123", "XYZ", "King", "Queen", "Cat", "Dog", "Ninja", "Wizard", "Dragon", "Phoenix" };

            for (int i = 0; i < count; i++)
            {
                string username = prefixes[rnd.Next(prefixes.Length)] + suffixes[rnd.Next(suffixes.Length)] + rnd.Next(1000);
                if (!Entrants.Contains(username))
                    Entrants.Add(username);
            }

            MessageBox.Show($"Added {count} random test entrants!");
        }

        private void StartRoll_Click(object sender, RoutedEventArgs e)
        {
            if (Entrants.Count == 0)
            {
                MessageBox.Show("No entrants yet!");
                return;
            }

            SaveSettings();

            Random rnd = new Random();
            int winnerIndex = rnd.Next(Entrants.Count);
            string winner = Entrants[winnerIndex];

            string prizeName;
            string prizeText;
            BitmapImage? prizeIcon = null;
            long amount = 1;

            if (_settings.CurrentRollMode == RollMode.ClassicWheel)
            {
                prizeName = ClassicPrizeNameText.Text;
                prizeText = _settings.WinnerPrizeTemplate.Replace("{amount}", "1").Replace("{prize}", prizeName);
                if (!string.IsNullOrEmpty(ClassicPrizeInputText.Text))
                    prizeIcon = new BitmapImage(new Uri(ClassicPrizeInputText.Text));

                var entrantsList = new List<string>(Entrants);
                //_overlay?.StartRolling(entrantsList, winner, winnerIndex, prizeIcon, prizeText);
                //_overlay?.StartScramble(entrantsList, winner);
                _overlay?.StartSlotMachine(entrantsList, winner);
            }
            else
            {
                // Bank modes – win is declared here so it's in scope for ConsumePrize and the rest
                PrizeWin? win = Bank.GetRandomPrize();
                if (win == null)
                {
                    MessageBox.Show("No prizes in the bank!");
                    return;
                }

                // NEW: Deplete the prize stack (only for items)
                Bank.ConsumePrize(win);
                SaveBank(); // persist the depletion immediately

                amount = win.WinAmount;
                prizeName = win.IsGold ? "Gold" : (win.CustomName ?? win.WinItem?.Name ?? "Prize");
                prizeText = _settings.WinnerPrizeTemplate.Replace("{amount}", amount.ToString()).Replace("{prize}", prizeName);

                string iconUrl = win.IsGold ? "pack://application:,,,/Images/Gold_coin.png" : (win.CustomIconUrl ?? win.WinItem?.Icon);
                if (!string.IsNullOrEmpty(iconUrl))
                    prizeIcon = new BitmapImage(new Uri(iconUrl));

                if (_settings.CurrentRollMode == RollMode.BankAnimation)
                {
                    if (_bankWindow == null || !_bankWindow.IsLoaded)
                    {
                        LoadBankFromFile();
                        Bank.Hydrate();
                        _bankWindow = new BankWindow(Bank, SaveBank, _settings.ShowPrizeRarityBadges);
                        _bankWindow.Show();
                    }
                    _bankWindow.StartRoll(win, winner);
                    _bankWindow.Activate();
                }
                else // BankRandom
                {
                    _overlay?.ShowBankWinner(winner, prizeIcon, prizeText);
                }
            }

            string chatPrize = amount > 1 ? $"{amount} × {prizeName}" : prizeName;
            if (_settings.CurrentRollMode != RollMode.ClassicWheel && prizeName == "Gold")
                chatPrize = $"{amount} Gold";

            string chatMsg = _settings.ChatTemplate
                .Replace("{winner}", winner)
                .Replace("{prize}", chatPrize)
                .Replace("{amount}", amount.ToString());

            _twitch?.SendMessage(chatMsg);
        }

        private async void OpenPrizeBank_Click(object sender, RoutedEventArgs e)
        {
            // Ensure item database is loaded (for icons/names in bank)
            if (Gw2ItemDatabase.Items.Count == 0)
            {
                try
                {
                    await Gw2ItemDatabase.LoadAsync(_httpClient);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load item database: {ex.Message}");
                    return;
                }
            }

            // Load the prize bank from file
            LoadBankFromFile();
            Bank.Hydrate();

            // Always create a fresh BankWindow instance (prevents "window closed" crash)
            _bankWindow = new BankWindow(Bank, SaveBank, _settings.ShowPrizeRarityBadges);

            // Clean up reference when bank window is closed
            _bankWindow.Closed += (s, ev) => _bankWindow = null;

            _bankWindow.Show();
            _bankWindow.Activate();
        }

        private void ClearEntrants_Click(object sender, RoutedEventArgs e)
        {
            Entrants.Clear();

            // Reset overlay to prize screen
            _overlay?.ResetToPrize();

            // Clear seen users for AllChatters mode (so everyone can enter again next giveaway)
            _twitch?.ClearSeenUsers();
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
                MessageBox.Show("Select a reward");
                return;
            }

            string username = RedeemUsernameText.Text.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Enter username");
                return;
            }

            switch (reward.Action)
            {
                case ChannelPointAction.AddToEntrants:
                    if (!Entrants.Contains(username))
                        Entrants.Add(username);
                    _twitch?.SendMessage($"@{username} redeemed {reward.Title} and joined the giveaway!");
                    break;

                case ChannelPointAction.InstantBankRoll:
                    InstantBankRollForUser(username);
                    break;

                case ChannelPointAction.InstantGoldWin:
                    if (reward.InstantGoldAmount > 0)
                    {
                        // You could add gold to a user database if you have one, or just announce
                        _twitch?.SendMessage($"@{username} redeemed {reward.Title} and won {reward.InstantGoldAmount} Gold!");
                    }
                    break;
            }

            RedeemUsernameText.Text = "";
        }

        private void InstantBankRollForUser(string username)
        {
            PrizeWin? win = Bank.GetRandomPrize();
            if (win == null)
            {
                _twitch?.SendMessage($"@{username} redeemed a channel point but no prizes left in bank!");
                return;
            }

            Bank.ConsumePrize(win);
            SaveBank();

            long amount = win.WinAmount;
            string prizeName = win.IsGold ? "Gold" : (win.CustomName ?? win.WinItem?.Name ?? "Prize");
            string prizeText = _settings.WinnerPrizeTemplate.Replace("{amount}", amount.ToString()).Replace("{prize}", prizeName);

            string iconUrl = win.IsGold ? "pack://application:,,,/Images/Gold_coin.png" : (win.CustomIconUrl ?? win.WinItem?.Icon);
            BitmapImage? prizeIcon = !string.IsNullOrEmpty(iconUrl) ? new BitmapImage(new Uri(iconUrl)) : null;

            // Show in overlay as instant winner
            _overlay?.ShowBankWinner(username, prizeIcon, prizeText);

            string chatPrize = amount > 1 ? $"{amount} × {prizeName}" : prizeName;
            if (prizeName == "Gold") chatPrize = $"{amount} Gold";

            _twitch?.SendMessage($"@{username} redeemed channel point and instantly won {chatPrize}! Congrats!");
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
                MessageBox.Show("Select a reward from the list to remove it.");
            }
        }
        private void StartEntries_Click(object sender, RoutedEventArgs e)
        {
            if (_entriesOpen)
            {
                MessageBox.Show("Entries already open!");
                return;
            }

            if (!int.TryParse(EntryTimeText.Text, out _entryTimeSeconds) || _entryTimeSeconds < 0)
                _entryTimeSeconds = 0;

            _entriesOpen = true;
            Entrants.Clear(); // fresh list for new period
            _twitch?._seenUsers.Clear();

            string timeMsg = _entryTimeSeconds > 0 ? $" for {TimeSpan.FromSeconds(_entryTimeSeconds):mm\\:ss} minutes" : " (unlimited)";
            _twitch?.SendMessage($"Giveaway entries OPEN{timeMsg}! Type {_settings.EntryCommand} to join!");

            EntryStatusText.Text = _entryTimeSeconds > 0 ? $"Entries Open – {_entryTimeSeconds}s remaining" : "Entries Open (unlimited)";
            EntryStatusText.Foreground = Brushes.LimeGreen;

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
                MessageBox.Show("Entries not open!");
                return;
            }

            _entriesOpen = false;
            _entryTimer?.Stop();

            _twitch?.SendMessage($"Giveaway entries CLOSED! Total entrants: {Entrants.Count}");

            EntryStatusText.Text = "Entries: Closed";
            EntryStatusText.Foreground = Brushes.Red;
        }

        private void EntryTimer_Tick(object? sender, EventArgs e)
        {
            _entryTimeSeconds--;
            if (_entryTimeSeconds <= 0)
            {
                StopEntries_Click(null, null); // auto-close
                return;
            }

            EntryStatusText.Text = $"Entries Open – {_entryTimeSeconds}s remaining";
        }

        // Updated Twitch_OnMessageReceived – wrap collection changes in Dispatcher.Invoke
        private void Twitch_OnMessageReceived(string username, string message)
        {
            if (!_entriesOpen) return; // only during open entry period

            username = username.ToLowerInvariant();

            Dispatcher.Invoke(() =>
            {
                if (_settings.EntryType == EntryMode.Command)
                {
                    if (message.Equals(_settings.EntryCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Entrants.Contains(username))
                            Entrants.Add(username);
                    }
                }
                else if (_settings.EntryType == EntryMode.AllChatters)
                {
                    if (!Entrants.Contains(username))
                        Entrants.Add(username);
                }
            });
        }


    }
}