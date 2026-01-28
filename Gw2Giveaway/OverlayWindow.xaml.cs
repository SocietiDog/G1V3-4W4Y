using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Gw2Giveaway
{
    public partial class OverlayWindow : Window
    {
        private double _currentAngle = 0;
        private List<Path> _segmentPaths = new();

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
            Hide(); // Hide instead of close – keeps instance alive for reuse
        }

        public void UpdatePrize(string name, string imageUrl)
        {
            PrizeTitleText.Text = $"Win: {name}";
            if (!string.IsNullOrEmpty(imageUrl))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imageUrl);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                PrizeImage.Source = bitmap;
            }
            else
            {
                PrizeImage.Source = null;
            }

            PrizeGrid.Visibility = Visibility.Visible;
            WheelGrid.Visibility = Visibility.Collapsed;
            WinnerGrid.Visibility = Visibility.Collapsed;
        }

        public void ResetToPrize()
        {
            UpdatePrize(PrizeTitleText.Text.Replace("Win: ", ""), PrizeImage.Source?.ToString() ?? "");
        }

        public void ShowBankWinner(string user, BitmapImage? icon, string prizeText)
        {
            PrizeGrid.Visibility = Visibility.Collapsed;
            WheelGrid.Visibility = Visibility.Collapsed;
            WinnerGrid.Visibility = Visibility.Visible;

            WinnerNameText.Text = $"@{user}";
            WinnerPrizeImage.Source = icon ?? new BitmapImage(new Uri("pack://application:,,,/Images/Gold_coin.png"));
            WinnerPrizeText.Text = prizeText;
        }

        // Updated StartRolling in OverlayWindow.cs – ensures wheel shows, builds, and animates properly
        public void StartRolling(List<string> entrants, string winner, int winnerIndex, BitmapImage? prizeIcon = null, string prizeText = "")
        {
            // Ensure modes
            PrizeGrid.Visibility = Visibility.Collapsed;
            WinnerGrid.Visibility = Visibility.Collapsed;
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
            PrizeGrid.Visibility = Visibility.Collapsed;
            WinnerGrid.Visibility = Visibility.Collapsed;
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
        // Updated StartSlotMachine – exact landing on winner in middle, no arrow
        // Updated StartSlotMachine – exact winner centering, uniform big boxes
        // Updated StartSlotMachine – perfect winner centering + name wrapping for long usernames
        public void StartSlotMachine(List<string> entrants, string winner, BitmapImage? prizeIcon = null, string prizeText = "")
        {
            PrizeGrid.Visibility = Visibility.Collapsed;
            WinnerGrid.Visibility = Visibility.Collapsed;
            SlotGrid.Visibility = Visibility.Visible;

            if (entrants.Count == 0) return;

            Random rnd = new Random();

            var extendedList = new List<string>();
            for (int i = 0; i < 30; i++)
                extendedList.AddRange(entrants);

            extendedList = extendedList.OrderBy(x => rnd.Next()).ToList();

            // Exact winner padding – lands perfectly in middle
            int winnerPads = 12; // adjust this if needed (higher = winner lower)
            for (int i = 0; i < winnerPads; i++)
                extendedList.Add(winner);

            extendedList.AddRange(entrants.Take(15)); // extra padding

            SlotReelContent.Children.Clear();

            double itemHeight = 140;

            foreach (string name in extendedList)
            {
                Border slot = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(15, 15, 35)),
                    CornerRadius = new CornerRadius(40),
                    Margin = new Thickness(40, 20, 40, 20),
                    Height = itemHeight
                };

                TextBlock tb = new TextBlock
                {
                    Text = name,
                    FontSize = 38,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.ExtraBold,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap, // wrap long names
                    MaxWidth = 320,
                    Effect = new DropShadowEffect { Color = Colors.Black, ShadowDepth = 4, BlurRadius = 12 }
                };

                slot.Child = tb;
                SlotReelContent.Children.Add(slot);
            }

            SlotReelContent.RenderTransform = new TranslateTransform(0, 0);

            // Perfect middle centering
            double viewHeight = 620;
            double middleOffset = viewHeight / 2 - itemHeight / 2;
            double winnerIndex = extendedList.Count - winnerPads - 8; // fine-tune this number for exact middle
            double finalY = -(winnerIndex * itemHeight) + middleOffset;

            DoubleAnimation anim = new DoubleAnimation(0, finalY, TimeSpan.FromSeconds(12));
            anim.EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut };

            Storyboard sb = new Storyboard();
            sb.Children.Add(anim);
            Storyboard.SetTarget(anim, SlotReelContent);
            Storyboard.SetTargetProperty(anim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

            sb.Completed += (_, __) =>
            {
                SlotWinnerHighlight.Opacity = 1;
                DoubleAnimation pulse = new DoubleAnimation(0.6, 1, TimeSpan.FromSeconds(0.4)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                SlotWinnerHighlight.BeginAnimation(OpacityProperty, pulse);

                Task.Delay(3000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        WinnerGrid.Visibility = Visibility.Visible;
                        SlotGrid.Visibility = Visibility.Collapsed;
                        WinnerNameText.Text = $"@{winner}";
                        WinnerPrizeImage.Source = prizeIcon ?? PrizeImage.Source;
                        WinnerPrizeText.Text = !string.IsNullOrWhiteSpace(prizeText) ? prizeText : PrizeTitleText.Text;
                    });
                });
            };

            sb.Begin();
        }
    }
}