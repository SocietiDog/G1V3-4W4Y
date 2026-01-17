using System;
using System.Threading.Tasks;  // ← Add this for await Task.Delay
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Gw2Giveaway
{
    public partial class BankWindow : Window
    {
        private readonly Action _saveCallback;
        private SlotViewModel _currentHighlightedSlot;
        public bool ShowPrizeRarityBadges { get; set; } = true;
        
        public BankWindow(PrizeBank bank, Action saveCallback, bool showRarityBadges = true)
        {
            InitializeComponent();
            ShowPrizeRarityBadges = showRarityBadges;
            _saveCallback = saveCallback;
            var vm = new BankViewModel(bank, _saveCallback);
            vm.ShowPrizeRarityBadges = showRarityBadges;
            DataContext = vm;
        }

        private void CloseApp_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void GoldText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var vm = (BankViewModel)DataContext;
            var dlg = new InputDialog("Set gold amount:", vm.GoldAmount.ToString());
            if (dlg.ShowDialog() == true && long.TryParse(dlg.Result, out long gold) && gold >= 0)
            {
                vm.GoldAmount = gold;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.OriginalSource is Border ||
                    e.OriginalSource is System.Windows.Shapes.Rectangle ||
                    e.OriginalSource is Grid)
                {
                    this.DragMove();
                }
            }
        }

        // Extra safety: force a final save when closing the window
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _saveCallback();  // calls SaveBank() one last time
            base.OnClosing(e);
        }

        public async void StartRoll(PrizeWin win, string user)
        {
            var vm = (BankViewModel)DataContext;

            var filledSlots = vm.Slots.Where(s => !string.IsNullOrEmpty(s.ImageSource)).ToList();

            WinnerNameText.Text = $"@{user}";

            if (win.IsGold)
            {
                WinnerPrizeImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/Gold_coin.png"));
                WinnerPrizeText.Text = $"{win.WinAmount} Gold";
            }
            else
            {
                string iconUrl = win.CustomIconUrl ?? win.WinItem?.Icon;
                if (!string.IsNullOrEmpty(iconUrl))
                {
                    WinnerPrizeImage.Source = new BitmapImage(new Uri(iconUrl));
                }
                else
                {
                    WinnerPrizeImage.Source = null; // or a placeholder if you want
                }

                string name = win.CustomName ?? win.WinItem?.Name ?? "Prize";
                string amountPrefix = win.WinAmount > 1 ? $"{win.WinAmount} × " : "";
                WinnerPrizeText.Text = amountPrefix + name;
            }

            // Clear previous highlight
            if (_currentHighlightedSlot != null)
            {
                _currentHighlightedSlot.IsHighlighted = false;
                _currentHighlightedSlot = null;
            }

            Random rnd = new Random();
            int spins = 20;
            int baseDelay = 150;
            for (int i = 0; i < spins; i++)
            {
                if (_currentHighlightedSlot != null)
                    _currentHighlightedSlot.IsHighlighted = false;

                if (filledSlots.Count > 0)
                {
                    _currentHighlightedSlot = filledSlots[rnd.Next(filledSlots.Count)];
                    _currentHighlightedSlot.IsHighlighted = true;
                }

                int delay = baseDelay + (i * 8);
                await Task.Delay(delay);
            }

            // Final winner highlight (only for items)
            if (!win.IsGold && win.Row >= 0 && win.Col >= 0)
            {
                var winningSlot = vm.Slots[win.Row * 10 + win.Col];
                winningSlot.IsHighlighted = true;
                _currentHighlightedSlot = winningSlot;

                await Task.Delay(1500);

                winningSlot.IsHighlighted = false;
                _currentHighlightedSlot = null;
            }
            else if (win.IsGold && filledSlots.Count > 0)
            {
                await Task.Delay(300);
            }

            WinnerGrid.Visibility = Visibility.Visible;
        }
    }
}