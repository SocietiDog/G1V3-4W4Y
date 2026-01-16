using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Gw2Giveaway
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<string> Entrants { get; } = new();
        private TwitchChat? _twitch;
        private OverlayWindow? _overlay;

        public PrizeBank Bank { get; private set; } = new PrizeBank();
        //private BankWindow? _bankWindow;

        private readonly HttpClient _httpClient = new HttpClient();

        // Save next to the exe in a "Data" subfolder (same as item cache)
        private static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string DataFolder = Path.Combine(AppDir, "Data");
       
        private static readonly string BankFile = Path.Combine(DataFolder, "prizebank.json");
       
        public MainWindow()
        {
            InitializeComponent();
            EntrantsList.ItemsSource = Entrants;

            // Load database with progress on startup
            LoadDatabaseWithProgress();

        }

        private async Task LoadDatabaseWithProgress(bool forceRefresh = false)
        {
            DatabaseStatus.Text = forceRefresh ? "Refreshing database..." : "Loading database...";
            DatabaseProgress.Value = 0;

            var progress = new Progress<int>(p =>
            {
                DatabaseProgress.Value = p;
                DatabaseStatus.Text = $"Loading: {p}%";
            });

            try
            {
                if (forceRefresh)
                {
                    string cacheFile = Path.Combine(DataFolder, "items.json");
                    if (File.Exists(cacheFile)) File.Delete(cacheFile);
                }

                await Gw2ItemDatabase.LoadAsync(_httpClient, progress);

                DatabaseStatus.Text = $"Ready ({Gw2ItemDatabase.Items.Count:N0} items)";
                DatabaseProgress.Value = 100;

                //LoadBankFromFile();
                Bank.Hydrate();
            }
            catch (Exception ex)
            {
                DatabaseStatus.Text = "Load failed";
                MessageBox.Show("Database error: " + ex.Message + "\nCheck internet.");
            }
        }

       
        // Refresh button – async void is fine for event handlers
        private async void RefreshDatabase_Click(object sender, RoutedEventArgs e)
        {
            await LoadDatabaseWithProgress(true);
        }
        private void LoadBankFromFile()
        {
            if (!File.Exists(BankFile))
            {
                Bank = new PrizeBank(); // constructor initializes the grid
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
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load prize bank (possibly old format). Starting with a fresh bank.\n" + ex.Message);
                Bank = new PrizeBank();
                SaveBank();
            }
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
            SaveBank();
            base.OnClosing(e);
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            _twitch = new TwitchChat
            {
                Channel = ChannelText.Text.ToLower(),
                BotName = BotNameText.Text.ToLower(),
                OAuth = "oauth:" + OAuthBox.Password,
                EntryCommand = EntryCommandText.Text
            };

            _twitch.NewEntrant += user => Dispatcher.Invoke(() => Entrants.Add(user));
            _twitch.Connect();
        }

        private async void FetchItem_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(ItemIdText.Text, out int id)) return;

            try
            {
                string json = await _httpClient.GetStringAsync($"https://api.guildwars2.com/v2/items/{id}");
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                string name = root.GetProperty("name").GetString()!;
                string icon = root.GetProperty("icon").GetString()!;

                PrizeNameText.Text = name;
                ImageUrlText.Text = icon;
                LoadPreview(icon);
                _overlay?.UpdatePrize(name, icon);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error fetching item: " + ex.Message);
            }
        }

        private void LoadPreview(string url)
        {
            if (!string.IsNullOrEmpty(url))
                PrizePreview.Source = new BitmapImage(new Uri(url));
        }

        private void ShowOverlay_Click(object sender, RoutedEventArgs e)
        {
            _overlay ??= new OverlayWindow();
            _overlay.UpdatePrize(PrizeNameText.Text, ImageUrlText.Text);
            _overlay.Show();
            _overlay.Activate();
        }

        private void StartRoll_Click(object sender, RoutedEventArgs e)
        {
            if (Entrants.Count == 0)
            {
                MessageBox.Show("No entrants yet!");
                return;
            }

            if (_overlay == null)
            {
                MessageBox.Show("Open the overlay first!");
                return;
            }

            Random rnd = new Random();
            int winnerIndex = rnd.Next(Entrants.Count);
            string winner = Entrants[winnerIndex];

            var entrantsList = new List<string>(Entrants);
            _overlay.StartRolling(entrantsList, winner, winnerIndex);

            _twitch?.SendMessage($"The winner is @{winner}! Congrats on the {PrizeNameText.Text}!");
        }

        private void ClearEntrants_Click(object sender, RoutedEventArgs e)
        {
            Entrants.Clear();
            _overlay?.ResetToPrize();
        }

        private void SelectClassicPrizeItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ItemSelectorDialog();
            if (dialog.ShowDialog() == true && dialog.SelectedItem != null)
            {
                PrizeNameText.Text = dialog.SelectedItem.Name;
                ImageUrlText.Text = dialog.SelectedItem.Icon;
                LoadPreview(ImageUrlText.Text);
                _overlay?.UpdatePrize(PrizeNameText.Text, ImageUrlText.Text);
            }
        }

        private async void OpenPrizeBank_Click(object sender, RoutedEventArgs e)
        {
            if (Gw2ItemDatabase.Items.Count == 0)
            {
                try
                {
                    await Gw2ItemDatabase.LoadAsync(_httpClient);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load item database: {ex.Message}");
                }
            }

            LoadBankFromFile();
            Bank.Hydrate();

            var bankWindow = new BankWindow(Bank, SaveBank);
            bankWindow.Show();
            bankWindow.Activate();
        }
        
        private void TestPrizeRoll_Click(object sender, RoutedEventArgs e)
        {
            LoadBankFromFile();
            Bank.Hydrate();
            var bankWindow = new BankWindow(Bank, SaveBank);
            bankWindow.Show();
            bankWindow.Activate();

            var userDlg = new InputDialog("Enter username for test roll:", "viewername");
            if (userDlg.ShowDialog() != true || string.IsNullOrWhiteSpace(userDlg.Result))
                return;

            string user = userDlg.Result.Trim();

            PrizeWin? win = Bank.GetRandomPrize();
            if (win == null)
            {
                MessageBox.Show("No prizes in the bank yet!");
                return;
            }

            string prizeDesc = win.IsGold ? $"{win.WinAmount} Gold" : $"{win.WinAmount} × {win.WinItem?.Name}";
            _twitch?.SendMessage($"@{user} redeemed and won {prizeDesc}! Congrats!");

            bankWindow.StartRoll(win, user);
        }
    }
}