using Gw2Giveaway.Services;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
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

        private bool _entriesOpen = false;
        private DispatcherTimer _entryTimer = new();
        private int _entryTimeSeconds = 300;

        public PrizeBank Bank { get; private set; } = new();

        private readonly HttpClient _httpClient = new();

        private const string DefaultTwitchClientId = "dlql0djuoozvkc81epya43ilibu19e";
        private const string TwitchOAuthRedirectUri = "http://localhost:54827/callback/";
        private const string TwitchOAuthScopes = "chat:read chat:edit channel:read:redemptions";

        private static readonly string DataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        private static readonly string BankFile = Path.Combine(DataFolder, "prizebank.json");
        private static readonly string SettingsFile = Path.Combine(DataFolder, "settings.json");

        private AppSettings _settings = new();
        private string _oauthAccessToken = string.Empty;

        private ObservableCollection<ChannelPointReward> _channelPointRewards = new();
        public ObservableCollection<ChannelPointReward> ChannelPointRewards => _channelPointRewards;
        public MainWindow()
        {
            Instance = this;
            InitializeComponent();

            try
            {
                if (EntrantsList != null)
                {
                    EntrantsList.ItemsSource = Entrants;
                }
                // Create TriviaViewModel with the shared TwitchChat instance
                Trivia = new TriviaViewModel(_twitch);

                try
                {
                    LoadSettings();
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("MainWindow.LoadSettings", ex);
                    _settings = new AppSettings();
                }

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

                        await _twitch.SendMessageAsync("Giveaway bot is online.");

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
                        MessageBox.Show($"Twitch connection failed: {error}");
                        ConnectButton.IsEnabled = true;
                        DisconnectButton.IsEnabled = false;

                        Trivia.IsTriviaEnabled = false;
                    });
                };

                _eventSub.OnRewardRedeemed += (username, title, rewardId, input, redemptionId) =>
                {
                    Dispatcher.Invoke(async () =>
                    {
                        if (title.Contains("Trivia", StringComparison.OrdinalIgnoreCase))
                        {
                            if (Trivia.IsTriviaEnabled && !Trivia.IsTriviaPaused)
                            {
                                Trivia.StartTriviaRound();
                                await _twitch.SendMessageAsync($"🤖Trivia-Tron: Trivia started by {username}'s redemption!");
                            }
                        }

                        if (string.IsNullOrWhiteSpace(_settings.TwitchOAuth) && !string.IsNullOrWhiteSpace(_settings.LegacyTwitchOAuth))
                        {
                            _settings.TwitchOAuth = _settings.LegacyTwitchOAuth;
                        }

                        if (string.IsNullOrWhiteSpace(_settings.TwitchClientId))
                        {
                            _settings.TwitchClientId = Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID") ?? string.Empty;
                        }
                    });
                };

                DataContext = this;

                _ = LoadDatabaseWithProgress();

                if (_settings.AutoOpenOverlay)
                    ShowTriviaOverlay();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MainWindow.Constructor", ex);
                MessageBox.Show($"Startup error: {ex.Message}", "Gw2Giveaway", MessageBoxButton.OK, MessageBoxImage.Error);
                DataContext = this;
            }
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

            _channelPointRewards = _settings.ChannelPointRewards != null
                ? new ObservableCollection<ChannelPointReward>(_settings.ChannelPointRewards.Where(r => r != null).Select(r => new ChannelPointReward
                {
                    Title = r!.Title,
                    Cost = r.Cost,
                    Action = r.Action,
                    InstantGoldAmount = r.InstantGoldAmount
                }))
                : new ObservableCollection<ChannelPointReward>();

            // Giveaway settings
            ChannelText.Text = _settings.TwitchChannel;
            BotNameText.Text = _settings.TwitchBotName;
            _oauthAccessToken = _settings.TwitchOAuth;
            BroadcasterIdText.Text = _settings.BroadcasterId;
            EntryModeCombo.SelectedIndex = (int)_settings.EntryType;
            EntryCommandText.Text = _settings.EntryCommand;
            ChatTemplateText.Text = _settings.ChatTemplate;
            WinnerPrizeTemplateText.Text = _settings.WinnerPrizeTemplate;
            GiveawayModeCombo.SelectedIndex = (int)_settings.CurrentGiveawayMode;
            BankPercentageSlider.Value = _settings.RandomPoolBankPercentage;
            UpdateBankPercentageDisplay();
            ShowRarityBadgesCheck.IsChecked = _settings.ShowPrizeRarityBadges;
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
            _settings.EntryType = (EntryMode)EntryModeCombo.SelectedIndex;
            _settings.EntryCommand = EntryCommandText.Text.Trim();
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
            _settings.ClassicUsePrizePool = !string.IsNullOrWhiteSpace(_settings.ClassicPrizePool);
            _settings.ChannelPointRewards = new ObservableCollection<ChannelPointReward>(_channelPointRewards.Select(r => new ChannelPointReward
            {
                Title = r.Title,
                Cost = r.Cost,
                Action = r.Action,
                InstantGoldAmount = r.InstantGoldAmount
            }));

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

            try
            {
                string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                Directory.CreateDirectory(DataFolder);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SaveSettings.WriteFile", ex);
            }
        }
        private void ShowTriviaOverlay_Click(object sender, RoutedEventArgs e) => ShowTriviaOverlay();

        private void ShowTriviaOverlay()
        {
            if (_triviaOverlay == null)
            {
                _triviaOverlay = new TriviaOverlayWindow(Trivia);
                _triviaOverlay.Closed += (s, e) => _triviaOverlay = null;
            }

            if (_triviaOverlay.IsVisible)
                _triviaOverlay.Hide();
            else
                _triviaOverlay.Show();
        }
       
        private void OpenTriviaSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(Trivia, this);
            settingsWindow.ShowDialog();
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
                AppLogger.LogError("LoadDatabaseWithProgress", ex);
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
                AppLogger.LogError("LoadBankFromFile", ex);
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
                AppLogger.LogError("SaveBank", ex);
                MessageBox.Show("Save failed: " + ex.Message + "\nPath: " + BankFile);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            SaveBank();

            _triviaOverlay?.Close();
            _overlay?.Close();
            _bankWindow?.Close();

            base.OnClosing(e);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void ShowRarityBadgesCheck_Changed(object sender, RoutedEventArgs e)
        {
            // Update the open Bank window if it exists
            if (_bankWindow != null)
            {
                bool show = ShowRarityBadgesCheck.IsChecked == true;
                _bankWindow.UpdateShowRarityBadges(show);
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void AuthWithTwitch_Click(object sender, RoutedEventArgs e)
        {
            string clientId = ResolveTwitchClientId();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                MessageBox.Show("Twitch login is not configured yet. Set TWITCH_CLIENT_ID (or save TwitchClientId in settings.json), then click Login with Twitch.");
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
                    if (string.IsNullOrWhiteSpace(ChannelText.Text))
                        ChannelText.Text = profile.Value.Login;

                    if (string.IsNullOrWhiteSpace(BotNameText.Text))
                        BotNameText.Text = profile.Value.Login;

                    if (string.IsNullOrWhiteSpace(BroadcasterIdText.Text))
                        BroadcasterIdText.Text = profile.Value.UserId;
                }

                SaveSettings();

                TwitchStatus.Text = "Authorized";
                TwitchStatus.Foreground = Brushes.LimeGreen;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("AuthWithTwitch_Click", ex);
                TwitchStatus.Text = "Authorization failed";
                TwitchStatus.Foreground = Brushes.Red;
                MessageBox.Show($"Twitch authorization failed: {ex.Message}");
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

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            if (string.IsNullOrWhiteSpace(ChannelText.Text) ||
                string.IsNullOrWhiteSpace(_oauthAccessToken) ||
                string.IsNullOrWhiteSpace(BroadcasterIdText.Text))
            {
                MessageBox.Show("Please fill in Channel and Broadcaster ID, then click Login with Twitch.");
                return;
            }

            TwitchStatus.Text = "Connecting...";
            TwitchStatus.Foreground = Brushes.Orange;

            try
            {
                _twitch.Channel = ChannelText.Text.Trim().ToLower();
                _twitch.BotName = BotNameText.Text.Trim().ToLower();
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
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private async void Disconnect_Click(object sender, RoutedEventArgs e)
        {
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
            var result = MessageBox.Show("Remove all entrants?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
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

            var result = MessageBox.Show("Clear all classic pool entries?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
            EnsureOverlayReady(showOverlay: true);
            _overlay.Show();
            _overlay.Activate();
        }

        private void EnsureOverlayReady(bool showOverlay)
        {
            _overlay ??= new OverlayWindow();
            ConfigureOverlayCallbacks();
            UpdateOverlayClassicPrizePreview();
            _overlay.UpdateEntryInstruction(_settings.EntryCommand);

            if (showOverlay && !_overlay.IsVisible)
            {
                _overlay.Show();
            }
        }

        private void UpdateOverlayClassicPrizePreview()
        {
            if (_overlay == null)
                return;

            var poolEntries = ParseClassicPrizePoolEntries(_settings.ClassicPrizePool);
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

            Trivia.IsTriviaPaused = true;
            SaveSettings();
            EnsureOverlayReady(showOverlay: true);

            var mode = _settings.CurrentGiveawayMode;

            // BANK ONLY - no animation, direct bank roll
            if (mode == GiveawayMode.BankOnly)
            {
                HandleBankRollDirect();
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
            var poolEntriesForRoll = ParseClassicPrizePoolEntries(_settings.ClassicPrizePool);

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
                        return;
                    }

                    string amountPrefix = prizeAmount > 1 ? $"{prizeAmount} × " : string.Empty;
                    string chatMsg = $"@{selectedWinner} won {amountPrefix}{prizeName}! Congratulations!";
                    _twitch?.SendMessageAsync(chatMsg);
                },
                winner,
                _settings.SlotRollDurationSeconds,
                isRandomMode,
                _settings.RandomPoolBankPercentage
            );
        }

        private async void HandleBankRollDirect()
        {
            await PerformBankRoll("Bank Roll", null);
        }

        private async void OpenPrizeBank_Click(object sender, RoutedEventArgs e)
        {
            // If window is already open, just activate it
            if (_bankWindow != null)
            {
                _bankWindow.Show();
                _bankWindow.Activate();
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
                    MessageBox.Show($"Failed to load item database: {ex.Message}");
                    return;
                }
            }

            // Save settings first to capture the current checkbox state
            SaveSettings();

            // Load the prize bank from file
            LoadBankFromFile();
            Bank.Hydrate();

            // Create a fresh BankWindow instance
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

                // Gather all filled slots
                List<(int row, int col)> filledSlots = new();
                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 10; c++)
                    {
                        var s = Bank.Slots[r, c];
                        bool hasPrize = s.Item != null || !string.IsNullOrEmpty(s.CustomName);
                        if (hasPrize && s.DisplayStack > 0)
                            filledSlots.Add((r, c));
                    }
                }

                if (filledSlots.Count == 0)
                {
                    MessageBox.Show("No items in the bank to roll!");
                    return;
                }

                // Pick a random filled slot
                Random rnd = new Random();
                var (selectedRow, selectedCol) = filledSlots[rnd.Next(filledSlots.Count)];
                var slot = Bank.Slots[selectedRow, selectedCol];

                var win = new PrizeWin
                {
                    IsGold = false,
                    WinAmount = slot.GiveawayAmount,
                    WinItem = slot.Item,
                    CustomName = slot.CustomName,
                    CustomIconUrl = slot.CustomIconUrl,
                    Row = selectedRow,
                    Col = selectedCol
                };

                // Open (or reuse) the bank window
                if (_bankWindow == null)
                {
                    if (Gw2ItemDatabase.Items.Count == 0)
                        await Gw2ItemDatabase.LoadAsync(_httpClient);

                    _bankWindow = new BankWindow(Bank, SaveBank, _settings.ShowPrizeRarityBadges);
                    _bankWindow.Closed += (s, ev) => _bankWindow = null;
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
                string prizeName = slot.Item?.Name ?? slot.CustomName ?? "Unknown";
                string amountPrefix = win.WinAmount > 1 ? $"{win.WinAmount} × " : "";
                await _twitch?.SendMessageAsync($"🏦 @{winnerName} won {amountPrefix}{prizeName}! Congrats!");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("PerformBankRoll", ex);
                MessageBox.Show($"Bank roll error: {ex.Message}");
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
            Trivia.IsTriviaPaused = true;
            if (!int.TryParse(EntryTimeText.Text, out _entryTimeSeconds) || _entryTimeSeconds < 0)
                _entryTimeSeconds = 0;

            _entriesOpen = true;
            Entrants.Clear(); // fresh list for new period
            

            string timeMsg = _entryTimeSeconds > 0 ? $" for {TimeSpan.FromSeconds(_entryTimeSeconds):mm\\:ss} minutes" : " (unlimited)";
            _twitch?.SendMessageAsync($"Giveaway entries OPEN{timeMsg}! Type {_settings.EntryCommand} to join!");

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

        private void GiveawayModeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return; // Avoid firing during initialization
            var mode = (GiveawayMode)GiveawayModeCombo.SelectedIndex;
            RandomPoolPanel.Visibility = (mode == GiveawayMode.RandomPool) ? Visibility.Visible : Visibility.Collapsed;
            SaveSettings();
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

        private readonly record struct TwitchOAuthToken(string AccessToken, string RefreshToken);
        private readonly record struct ClassicPrizePoolEntry(string Name, string IconUrl, long Amount);


    }
}
