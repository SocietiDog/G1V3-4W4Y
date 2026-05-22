using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Gw2Giveaway
{
    public partial class OverlayWindow : Window
    {
        private double _currentAngle = 0;
        private List<Path> _segmentPaths = new();
        private int _slotRunId = 0; // Track slot machine runs
        private DispatcherTimer? _winnerPrizeCarouselTimer;
        private DispatcherTimer? _prizePreviewCarouselTimer;

        // Callback for when winner is revealed: passes (winnerName, isFromBank)
        public Action<string, bool>? OnSlotWinnerRevealed { get; set; }

        public OverlayWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void HideAllGrids()
        {
            _winnerPrizeCarouselTimer?.Stop();
            _prizePreviewCarouselTimer?.Stop();
            PrizeGrid.Visibility = Visibility.Collapsed;
            SlotGrid.Visibility = Visibility.Collapsed;
            ScrambleGrid.Visibility = Visibility.Collapsed;
            WheelGrid.Visibility = Visibility.Collapsed;
            WinnerGrid.Visibility = Visibility.Collapsed;
        }

        public void UpdatePrize(string name, string imageUrl)
        {
            _prizePreviewCarouselTimer?.Stop();
            PrizeTitleText.Text = $"Win: {name}";
            if (!string.IsNullOrEmpty(imageUrl))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imageUrl);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                if (bitmap.CanFreeze)
                    bitmap.Freeze();
                PrizeImage.Source = bitmap;
            }
            else
            {
                PrizeImage.Source = null;
            }

            HideAllGrids();
            PrizeGrid.Visibility = Visibility.Visible;
        }

        public void ResetToPrize()
        {
            UpdatePrize(PrizeTitleText.Text.Replace("Win: ", ""), PrizeImage.Source?.ToString() ?? "");
        }

        public void ShowBankWinner(string user, BitmapImage? icon, string prizeText)
        {
            _winnerPrizeCarouselTimer?.Stop();
            HideAllGrids();
            WinnerGrid.Visibility = Visibility.Visible;
            PrizePoolPanel.Visibility = Visibility.Collapsed;

            WinnerNameText.Text = $"@{user}";
            WinnerPrizeImage.Source = icon ?? new BitmapImage(new Uri("pack://application:,,,/Images/Gold_coin.png"));
            WinnerPrizeText.Text = prizeText;
        }

        public void StartWinnerPrizeCarousel(List<(BitmapImage? Icon, string Text)> prizes, int intervalMs = 1800)
        {
            _winnerPrizeCarouselTimer?.Stop();

            if (prizes == null || prizes.Count <= 1)
                return;

            int index = 0;
            _winnerPrizeCarouselTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Math.Max(900, intervalMs))
            };

            _winnerPrizeCarouselTimer.Tick += (_, __) =>
            {
                index = (index + 1) % prizes.Count;
                WinnerPrizeImage.Source = prizes[index].Icon ?? PrizeImage.Source;
                WinnerPrizeText.Text = prizes[index].Text;
            };

            _winnerPrizeCarouselTimer.Start();
        }

        public void StartPrizePreviewCarousel(List<(BitmapImage? Icon, string Text)> prizes, int intervalMs = 1800)
        {
            _prizePreviewCarouselTimer?.Stop();

            if (prizes == null || prizes.Count <= 1)
                return;

            int index = 0;
            _prizePreviewCarouselTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Math.Max(900, intervalMs))
            };

            _prizePreviewCarouselTimer.Tick += (_, __) =>
            {
                index = (index + 1) % prizes.Count;
                PrizeImage.Source = prizes[index].Icon;
                PrizeTitleText.Text = prizes[index].Text.StartsWith("Win:", StringComparison.OrdinalIgnoreCase)
                    ? prizes[index].Text
                    : $"Win: {prizes[index].Text}";
            };

            _prizePreviewCarouselTimer.Start();
        }

        // Updated StartRolling in OverlayWindow.cs – ensures wheel shows, builds, and animates properly
        public void StartRolling(List<string> entrants, string winner, int winnerIndex, BitmapImage? prizeIcon = null, string prizeText = "")
        {
            HideAllGrids();
            WheelGrid.Visibility = Visibility.Visible;

            // Build the wheel (critical – populates RotatableCanvas)
            BuildWheel(entrants);

            if (entrants.Count == 0) return;

            double angleStep = 360.0 / entrants.Count;
            double winnerMidAngle = winnerIndex * angleStep + angleStep / 2;

            Random rnd = new Random();
            int extraSpins = 6 + rnd.Next(6); // 6-11 full spins for excitement
            double spinAmount = extraSpins * 360 + winnerMidAngle;

            double from = _currentAngle;
            double to = _currentAngle + spinAmount;

            // Smooth easing – long spin with slow stop
            DoubleAnimation anim = new DoubleAnimation(from, to, TimeSpan.FromSeconds(10));
            anim.EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut };

            Storyboard sb = new Storyboard();
            Storyboard.SetTarget(anim, RotatableCanvas);
            Storyboard.SetTargetProperty(anim, new PropertyPath("RenderTransform.Angle"));
            sb.Children.Add(anim);

            sb.Completed += (_, __) =>
            {
                _currentAngle = to % 360;

                // Highlight winning segment
                if (winnerIndex < _segmentPaths.Count)
                    _segmentPaths[winnerIndex].Fill = Brushes.Gold;

                // Switch to winner screen
                WinnerGrid.Visibility = Visibility.Visible;
                WheelGrid.Visibility = Visibility.Collapsed;

                WinnerNameText.Text = $"@{winner}";
                WinnerPrizeImage.Source = prizeIcon ?? PrizeImage.Source;
                WinnerPrizeText.Text = !string.IsNullOrEmpty(prizeText) ? prizeText : PrizeTitleText.Text;
            };

            sb.Begin();
        }

        // Updated BuildWheel – ensures segments/text visible, better scaling
        private void BuildWheel(List<string> entrants)
        {
            RotatableCanvas.Children.Clear();
            _segmentPaths.Clear();

            if (entrants.Count == 0) return;

            double canvasSize = 400; // Match Canvas Width/Height in XAML
            double centerX = canvasSize / 2;
            double centerY = canvasSize / 2;
            double radius = canvasSize / 2 - 20;

            double angleStep = 360.0 / entrants.Count;

            List<Color> colors = new()
    {
        Colors.RoyalBlue, Colors.Crimson, Colors.ForestGreen, Colors.OrangeRed,
        Colors.Purple, Colors.DeepPink, Colors.DarkGoldenrod, Colors.MediumVioletRed
    };

            for (int i = 0; i < entrants.Count; i++)
            {
                double startAngle = i * angleStep;
                Color color = colors[i % colors.Count];
                SolidColorBrush brush = new SolidColorBrush(color) { Opacity = 0.9 };

                StreamGeometry geo = new StreamGeometry();
                using (StreamGeometryContext ctx = geo.Open())
                {
                    Point center = new(centerX, centerY);
                    Point p1 = GetPoint(centerX, centerY, radius, startAngle);
                    Point p2 = GetPoint(centerX, centerY, radius, startAngle + angleStep);

                    ctx.BeginFigure(center, true, true);
                    ctx.LineTo(p1, true, false);
                    ctx.ArcTo(p2, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                    ctx.LineTo(center, true, false);
                }

                Path segment = new Path { Data = geo, Fill = brush, Stroke = Brushes.Black, StrokeThickness = 4 };
                RotatableCanvas.Children.Add(segment);
                _segmentPaths.Add(segment);

                // Username – smaller, readable
                string name = entrants[i];
                double textAngle = startAngle + angleStep / 2;
                double textRadius = radius * 0.7;

                double fontSize = Math.Max(12, Math.Min(20, 50 - entrants.Count)); // caps at 20pt

                TextBlock tb = new TextBlock
                {
                    Text = name,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = fontSize,
                    Effect = new DropShadowEffect { Color = Colors.Black, ShadowDepth = 2, BlurRadius = 4 }
                };

                double rotation = textAngle > 90 && textAngle < 270 ? textAngle + 180 : textAngle;
                tb.RenderTransform = new RotateTransform(rotation);
                tb.RenderTransformOrigin = new Point(0.5, 0.5);

                tb.Measure(new Size(canvasSize, canvasSize));
                Size size = tb.DesiredSize;

                Point textPos = GetPoint(centerX, centerY, textRadius, textAngle);
                Canvas.SetLeft(tb, textPos.X - size.Width / 2);
                Canvas.SetTop(tb, textPos.Y - size.Height / 2);

                RotatableCanvas.Children.Add(tb);
            }
        }
        public void UpdateEntryInstruction(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                command = "!enter"; // fallback

            EntryInstructionText.Text = $"Type {command} in chat to join";
        }
        private Point GetPoint(double cx, double cy, double r, double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            return new Point(cx + r * Math.Sin(rad), cy - r * Math.Cos(rad));
        }
        // New StartScramble method in OverlayWindow.cs (call instead of StartRolling for classic mode)
        public void StartScramble(List<string> entrants, string winner, BitmapImage? prizeIcon = null, string prizeText = "")
        {
            HideAllGrids();
            ScrambleGrid.Visibility = Visibility.Visible;

            ScrambleItems.ItemsSource = entrants;

            Random rnd = new Random();
            int flashes = 50 + rnd.Next(30); // 50-80 rapid flashes
            int delay = 50; // starting fast

            Task.Run(async () =>
            {
                for (int i = 0; i < flashes; i++)
                {
                    int randomIndex = rnd.Next(entrants.Count);
                    string randomName = entrants[randomIndex];

                    await Dispatcher.InvokeAsync(() =>
                    {
                        // Highlight random name (simple opacity pulse – you can add background color change)
                        foreach (Border border in FindVisualChildren<Border>(ScrambleItems))
                        {
                            var tb = border.Child as TextBlock;
                            if (tb?.Text == randomName)
                                border.Opacity = 1;
                            else
                                border.Opacity = 0.5;
                        }
                    });

                    await Task.Delay(delay);
                    delay += 10; // slow down
                }

                // Final winner
                await Dispatcher.InvokeAsync(() =>
                {
                    foreach (Border border in FindVisualChildren<Border>(ScrambleItems))
                    {
                        var tb = border.Child as TextBlock;
                        if (tb?.Text == winner)
                        {
                            border.Background = Brushes.Gold;
                            border.Opacity = 1;
                            tb.FontSize = 40; // big winner
                            tb.Foreground = Brushes.Black;
                        }
                        else
                        {
                            border.Opacity = 0.3;
                        }
                    }

                    // Switch to winner screen after delay
                    Task.Delay(2000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            WinnerGrid.Visibility = Visibility.Visible;
                            ScrambleGrid.Visibility = Visibility.Collapsed;
                            WinnerNameText.Text = $"@{winner}";
                            WinnerPrizeImage.Source = prizeIcon ?? PrizeImage.Source;
                            WinnerPrizeText.Text = !string.IsNullOrEmpty(prizeText) ? prizeText : PrizeTitleText.Text;// prize icon/text as before
                        });
                    });
                });
            });
        }

        // Helper to find children (add to OverlayWindow.cs)
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                        yield return (T)child;

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                        yield return childOfChild;
                }
            }
        }

        // Updated StartSlotMachine – perfect winner centering with enhanced visuals
        public void StartSlotMachine(
            List<string> entrants, 
            BitmapImage? prizeIcon = null, 
            string prizeText = "", 
            Action<string>? onChatAnnounce = null, 
            string? forcedWinner = null,
            int rollDurationSeconds = 12,
            bool isRandomPoolMode = false,
            int bankPercentage = 50)
        {
            if (entrants == null || entrants.Count == 0)
                return;

            rollDurationSeconds = Math.Clamp(rollDurationSeconds, 4, 30);

            // Set UI visibility
            HideAllGrids();
            SlotGrid.Visibility = Visibility.Visible;
            SlotWinnerHighlight.Opacity = 0;  // Start invisible

            // Track run ID to prevent cross-run races
            int runId = ++_slotRunId;

            // Scale duplicates based on roll duration
            double durationScaleFactor = rollDurationSeconds / 12.0;
            int duplicates = Math.Max(40, (int)Math.Ceiling(60 * durationScaleFactor));

            // Build extended list (multiple duplicates of each entrant, shuffled)
            var extendedList = new List<string>();
            for (int i = 0; i < duplicates; i++)
                extendedList.AddRange(entrants);

            Random rnd = new Random();
            extendedList = extendedList.OrderBy(x => rnd.Next()).ToList();

            // Create visual items with enhanced styling
            double itemHeight = 160;
            double itemSpacing = itemHeight + 20;  // 180px total per item

            SlotReelContent.Children.Clear();

            // Color palette for visual variety
            Color[] colors = new[]
            {
                Color.FromRgb(20, 30, 80),    // Dark blue
                Color.FromRgb(40, 20, 60),    // Dark purple
                Color.FromRgb(20, 60, 40),    // Dark green
                Color.FromRgb(80, 30, 20),    // Dark red
            };

            int colorIndex = 0;
            foreach (string name in extendedList)
            {
                Border box = new Border
                {
                    Background = new SolidColorBrush(colors[colorIndex % colors.Length]),
                    CornerRadius = new CornerRadius(15),
                    Margin = new Thickness(15, 10, 15, 10),
                    Height = itemHeight,
                    Width = 440,
                    BorderBrush = Brushes.Gold,
                    BorderThickness = new Thickness(3),
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Gold,
                        ShadowDepth = 0,
                        BlurRadius = 10,
                        Opacity = 0.3
                    }
                };

                TextBlock tb = new TextBlock
                {
                    Text = name,
                    FontSize = 52,
                    Foreground = Brushes.Gold,
                    FontWeight = FontWeights.ExtraBold,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 400,
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        ShadowDepth = 5,
                        BlurRadius = 10,
                        Opacity = 1.0
                    }
                };

                box.Child = tb;
                SlotReelContent.Children.Add(box);
                colorIndex++;
            }

            // Compute animation target
            double viewHeight = 620;
            double listHeight = extendedList.Count * itemSpacing;
            double middleOffset = viewHeight / 2 - itemHeight / 2 - 10;

            double maxY = -(listHeight - viewHeight);
            if (maxY >= 0)
                return;

            if (SlotReelContent.RenderTransform is TranslateTransform resetTransform)
                resetTransform.Y = 0;
            int minTravelItems = Math.Min(extendedList.Count - 1, Math.Max(12, entrants.Count * 3));
            int targetIndex;

            if (!string.IsNullOrEmpty(forcedWinner))
            {
                var forcedIndices = extendedList
                    .Select((name, idx) => new { name, idx })
                    .Where(x => string.Equals(x.name, forcedWinner, StringComparison.OrdinalIgnoreCase) && x.idx >= minTravelItems)
                    .Select(x => x.idx)
                    .ToList();

                if (forcedIndices.Count > 0)
                {
                    int lateStart = forcedIndices.Count / 2;
                    targetIndex = forcedIndices[rnd.Next(lateStart, forcedIndices.Count)];
                }
                else
                {
                    targetIndex = rnd.Next(minTravelItems, extendedList.Count);
                }
            }
            else
            {
                targetIndex = rnd.Next(minTravelItems, extendedList.Count);
            }

            double randomStop = -(targetIndex * itemSpacing) + middleOffset;
            randomStop = Math.Max(maxY, Math.Min(0, randomStop));

            // Animate the reel with smooth easing
            DoubleAnimation anim = new DoubleAnimation(0, randomStop, 
                TimeSpan.FromSeconds(rollDurationSeconds))
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            };

            Storyboard sb = new Storyboard();
            sb.Children.Add(anim);
            Storyboard.SetTarget(anim, SlotReelContent);
            Storyboard.SetTargetProperty(anim, 
                new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

            sb.Completed += async (_, __) =>
            {
                // Pulse animation for winner highlight
                DoubleAnimation pulse = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                pulse.AutoReverse = true;
                pulse.RepeatBehavior = new RepeatBehavior(3);
                SlotWinnerHighlight.BeginAnimation(OpacityProperty, pulse);
                string winner = extendedList[targetIndex];

                // Short pause, then reveal winner
                await Task.Delay(900);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (runId != _slotRunId) return;

                    // Show winner screen
                    HideAllGrids();
                    WinnerGrid.Visibility = Visibility.Visible;
                    WinnerNameText.Text = $"@{winner}";
                    WinnerPrizeImage.Source = prizeIcon ?? PrizeImage.Source;
                    WinnerPrizeText.Text = prizeText;

                    if (isRandomPoolMode)
                    {
                        // Show the BANK/PRIZE pool panel and run bounce animation
                        PrizePoolPanel.Visibility = Visibility.Visible;
                        RunBounceAnimation(winner, bankPercentage, onChatAnnounce);
                    }
                    else
                    {
                        // Prize Only mode — just show the winner, no bank/prize boxes
                        PrizePoolPanel.Visibility = Visibility.Collapsed;
                        onChatAnnounce?.Invoke(winner);

                        // Auto-close overlay after showing winner for a few seconds
                        AutoCloseAfterDelay(5000);
                    }
                });
            };

            sb.Begin();
        }

        private int _bounceRunId = 0;

        /// <summary>
        /// Bounce animation between BANK and PRIZE boxes, then land on the selected pool.
        /// After landing, fires the appropriate callback and closes overlay if bank was picked.
        /// </summary>
        private async void RunBounceAnimation(string winner, int bankPercentage, Action<string>? onChatAnnounce)
        {
            int bounceId = ++_bounceRunId;

            // Reset both glows
            BankPoolGlow.BeginAnimation(OpacityProperty, null);
            DisplayedPoolGlow.BeginAnimation(OpacityProperty, null);
            BankPoolGlow.Opacity = 0;
            DisplayedPoolGlow.Opacity = 0;

            // Determine outcome based on bank percentage
            Random rnd = new Random();
            bool landOnBank = rnd.Next(100) < bankPercentage;

            // Bounce back and forth quickly, then land
            int bounces = 4 + rnd.Next(2);
            int delay = 80;

            for (int i = 0; i < bounces; i++)
            {
                if (bounceId != _bounceRunId) return;

                bool showBank = (i % 2 == 0);
                // On last bounce, land on the actual result
                if (i == bounces - 1)
                    showBank = landOnBank;

                await Dispatcher.InvokeAsync(() =>
                {
                    if (showBank)
                    {
                        BankPoolBox.BorderBrush = new SolidColorBrush(Colors.Gold);
                        BankPoolBox.BorderThickness = new Thickness(5);
                        BankPoolGlow.Opacity = 0.8;
                        DisplayedPoolBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x33));
                        DisplayedPoolBox.BorderThickness = new Thickness(3);
                        DisplayedPoolGlow.Opacity = 0;
                    }
                    else
                    {
                        DisplayedPoolBox.BorderBrush = new SolidColorBrush(Colors.Gold);
                        DisplayedPoolBox.BorderThickness = new Thickness(5);
                        DisplayedPoolGlow.Opacity = 0.8;
                        BankPoolBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x66));
                        BankPoolBox.BorderThickness = new Thickness(3);
                        BankPoolGlow.Opacity = 0;
                    }
                });

                await Task.Delay(delay);
                delay += 12; // slight progressive slowdown
            }

            if (bounceId != _bounceRunId) return;

            // Final dramatic glow on the winner
            await Dispatcher.InvokeAsync(() =>
            {
                if (landOnBank)
                {
                    var bankGlowAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600));
                    bankGlowAnim.EasingFunction = new PowerEase { EasingMode = EasingMode.EaseOut };
                    BankPoolGlow.BeginAnimation(OpacityProperty, bankGlowAnim);
                }
                else
                {
                    var prizeGlowAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600));
                    prizeGlowAnim.EasingFunction = new PowerEase { EasingMode = EasingMode.EaseOut };
                    DisplayedPoolGlow.BeginAnimation(OpacityProperty, prizeGlowAnim);
                }
            });

            // Brief pause to show final pool result
            await Task.Delay(350);
            if (bounceId != _bounceRunId) return;

            // Fire the unified callback with winner name and bank selection
            Dispatcher.Invoke(() =>
            {
                OnSlotWinnerRevealed?.Invoke(winner, landOnBank);

                if (landOnBank)
                {
                    // Bank was picked — close the overlay (bank window will open from callback)
                    Close();
                }
                else
                {
                    // Prize was picked — send chat message and auto-close after delay
                    onChatAnnounce?.Invoke(winner);
                    AutoCloseAfterDelay(5000);
                }
            });
        }

        private async void AutoCloseAfterDelay(int milliseconds)
        {
            await Task.Delay(milliseconds);
            Dispatcher.Invoke(() =>
            {
                Close();
            });
        }
    }
}