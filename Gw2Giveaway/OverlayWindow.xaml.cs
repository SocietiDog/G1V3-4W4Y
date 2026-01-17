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

        public void StartRolling(List<string> entrants, string winner, int winnerIndex, BitmapImage? prizeIcon = null, string prizeText = "")
        {
            PrizeGrid.Visibility = Visibility.Collapsed;
            WinnerGrid.Visibility = Visibility.Collapsed;
            WheelGrid.Visibility = Visibility.Visible;

            BuildWheel(entrants);

            double angleStep = 360.0 / entrants.Count;
            double winnerMidAngle = winnerIndex * angleStep + angleStep / 2;

            Random rnd = new Random();
            int extraSpins = 6 + rnd.Next(6);
            double spinAmount = extraSpins * 360 + winnerMidAngle;

            double from = _currentAngle;
            double to = _currentAngle + spinAmount;

            DoubleAnimation anim = new DoubleAnimation(from, to, TimeSpan.FromSeconds(8));
            anim.EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut };

            Storyboard sb = new Storyboard();
            Storyboard.SetTarget(anim, RotatableCanvas);
            Storyboard.SetTargetProperty(anim, new PropertyPath("RenderTransform.Angle"));
            sb.Children.Add(anim);

            sb.Completed += (_, __) =>
            {
                _currentAngle = to % 360;

                if (winnerIndex < _segmentPaths.Count)
                    _segmentPaths[winnerIndex].Fill = Brushes.Gold;

                WinnerGrid.Visibility = Visibility.Visible;
                WheelGrid.Visibility = Visibility.Collapsed;

                WinnerNameText.Text = $"@{winner}";
                WinnerPrizeImage.Source = prizeIcon ?? PrizeImage.Source;
                WinnerPrizeText.Text = !string.IsNullOrEmpty(prizeText) ? prizeText : PrizeTitleText.Text;
            };

            sb.Begin();
        }

        private void BuildWheel(List<string> entrants)
        {
            RotatableCanvas.Children.Clear();
            _segmentPaths.Clear();

            if (entrants.Count == 0) return;

            double centerX = 500;
            double centerY = 500;
            double radius = 480;

            double angleStep = 360.0 / entrants.Count;

            // Better color palette (GW2-inspired)
            List<Color> colors = new()
            {
                Colors.RoyalBlue, Colors.Crimson, Colors.ForestGreen, Colors.OrangeRed,
                Colors.Purple, Colors.DeepPink, Colors.DarkOrange, Colors.MediumVioletRed
            };

            for (int i = 0; i < entrants.Count; i++)
            {
                double startAngle = i * angleStep;
                Color color = colors[i % colors.Count];
                SolidColorBrush brush = new SolidColorBrush(color);

                // Segment geometry
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

                // Username text
                string name = entrants[i];
                double textAngle = startAngle + angleStep / 2;
                double textRadius = radius * 0.75;

                TextBlock tb = new TextBlock
                {
                    Text = name,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = Math.Max(16, 48 - entrants.Count / 2),
                    Effect = new DropShadowEffect { Color = Colors.Black, ShadowDepth = 2, BlurRadius = 4 }
                };

                double rotation = (textAngle > 90 && textAngle < 270) ? textAngle + 180 : textAngle;
                tb.RenderTransform = new RotateTransform(rotation);
                tb.RenderTransformOrigin = new Point(0.5, 0.5);

                tb.Measure(new Size(400, 100));
                Size size = tb.DesiredSize;

                Point textPos = GetPoint(centerX, centerY, textRadius, textAngle);
                Canvas.SetLeft(tb, textPos.X - size.Width / 2);
                Canvas.SetTop(tb, textPos.Y - size.Height / 2);

                RotatableCanvas.Children.Add(tb);
            }
        }

        private Point GetPoint(double cx, double cy, double r, double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            return new Point(cx + r * Math.Sin(rad), cy - r * Math.Cos(rad));
        }
    }
}