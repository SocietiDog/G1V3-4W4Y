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
        
        private PrizeBank _bank;

        public BankWindow(PrizeBank bank, Action saveCallback, bool showRarityBadges = true)
        {
            InitializeComponent();
            ShowPrizeRarityBadges = showRarityBadges;
            _saveCallback = saveCallback;
            _bank = bank;
            var vm = CreateViewModel(bank, showRarityBadges);
            DataContext = vm;

            // Apply the correct grid dimensions once the visual tree is ready
            Loaded += (_, _) => ApplyGridDimensions(_bank.Rows, _bank.Cols);
        }

        private BankViewModel CreateViewModel(PrizeBank bank, bool showRarityBadges)
        {
            return new BankViewModel(bank, _saveCallback, showRarityBadges, onResize: (rows, cols) =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Rebuild ViewModel with new grid dimensions
                    DataContext = CreateViewModel(_bank, ShowPrizeRarityBadges);
                    // Defer grid dimension update until after the new panel is measured/arranged
                    Dispatcher.InvokeAsync(() => ApplyGridDimensions(rows, cols),
                        System.Windows.Threading.DispatcherPriority.Loaded);
                });
            });
        }

        private void ApplyGridDimensions(int rows, int cols)
        {
            // Walk the visual tree to find the UniformGrid inside the ItemsControl
            SlotsItemsControl.ApplyTemplate();
            var panel = SlotsItemsControl.ItemsPanel;
            if (FindUniformGrid(SlotsItemsControl) is System.Windows.Controls.Primitives.UniformGrid ug)
            {
                ug.Rows = rows;
                ug.Columns = cols;
            }
        }

        private static System.Windows.Controls.Primitives.UniformGrid? FindUniformGrid(DependencyObject parent)
        {
            if (parent == null) return null;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.Primitives.UniformGrid ug) return ug;
                var result = FindUniformGrid(child);
                if (result != null) return result;
            }
            return null;
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

        public void UpdateShowRarityBadges(bool show)
        {
            var vm = (BankViewModel)DataContext;
            vm.ShowPrizeRarityBadges = show;
            foreach (var slot in vm.Slots)
            {
                slot.ShowPrizeRarityBadges = show;
            }
        }

        public async Task StartRollAsync(PrizeWin win, string user, int durationSeconds = 5)
        {
            var vm = (BankViewModel)DataContext;

            var filledSlots = vm.Slots.Where(s => !string.IsNullOrEmpty(s.ImageSource)).ToList();

            WinnerNameText.Text = $"@{user}";
            WinnerGrid.Visibility = Visibility.Collapsed;

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
                    WinnerPrizeImage.Source = null;
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

            // Scale spin count and timing based on duration
            // Target total time ≈ durationSeconds * 1000 ms
            // Each spin i takes: baseDelay + i * rampUp
            // Total = spins * baseDelay + rampUp * (spins*(spins-1)/2)
            // Solve for spins given duration
            durationSeconds = Math.Clamp(durationSeconds, 2, 15);
            int targetMs = durationSeconds * 1000;
            int baseDelay = 80;
            int rampUp = 12;

            // Calculate how many spins fit: sum = n*baseDelay + rampUp*n*(n-1)/2 ≈ targetMs
            // n*80 + 6*n*(n-1) ≈ targetMs → 6n² + 74n ≈ targetMs
            // Solve quadratic: n = (-74 + sqrt(74² + 4*6*targetMs)) / (2*6)
            int spins = (int)((-74 + Math.Sqrt(74 * 74 + 24.0 * targetMs)) / 12.0);
            spins = Math.Clamp(spins, 8, 80);

            Random rnd = new Random();
            for (int i = 0; i < spins; i++)
            {
                if (_currentHighlightedSlot != null)
                    _currentHighlightedSlot.IsHighlighted = false;

                if (filledSlots.Count > 0)
                {
                    _currentHighlightedSlot = filledSlots[rnd.Next(filledSlots.Count)];
                    _currentHighlightedSlot.IsHighlighted = true;
                }

                int delay = baseDelay + (i * rampUp);
                await Task.Delay(delay);
            }

            // Final winner highlight (only for items)
            if (!win.IsGold && win.Row >= 0 && win.Col >= 0)
            {
                if (_currentHighlightedSlot != null)
                {
                    _currentHighlightedSlot.IsHighlighted = false;
                }

                var winningSlot = vm.Slots[win.Row * vm.GridCols + win.Col];
                winningSlot.IsHighlighted = true;
                _currentHighlightedSlot = winningSlot;

                await Task.Delay(500);

                winningSlot.IsHighlighted = false;
                _currentHighlightedSlot = null;
            }
            else if (win.IsGold && filledSlots.Count > 0)
            {
                await Task.Delay(150);
            }

            WinnerGrid.Visibility = Visibility.Visible;
        }
    }
}