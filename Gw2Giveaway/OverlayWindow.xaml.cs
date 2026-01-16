using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

        public void UpdatePrize(string name, string imageUrl)
        {
            PrizeTitleText.Text = $"Win: {name}";
            if (!string.IsNullOrEmpty(imageUrl))
                PrizeImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(imageUrl));

            PrizeGrid.Visibility = Visibility.Visible;
            WheelGrid.Visibility = Visibility.Collapsed;
            WinnerGrid.Visibility = Visibility.Collapsed;
        }

        public void ResetToPrize()
        {
            UpdatePrize(PrizeTitleText.Text.Replace("Win: ", ""), PrizeImage.Source?.ToString() ?? "");
        }

        public void StartRolling(List<string> entrants, string winner, int winnerIndex)
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

                // Switch to winner screen
                WinnerGrid.Visibility = Visibility.Visible;
                WheelGrid.Visibility = Visibility.Collapsed;
                WinnerNameText.Text = $"@{winner}";
                WinnerPrizeImage.Source = PrizeImage.Source;
                WinnerPrizeText.Text = PrizeTitleText.Text;
            };

            sb.Begin();
        }

        private void BuildWheel(List<string> entrants)
        {
            RotatableCanvas.Children.Clear();
            _segmentPaths.Clear();

            if (entrants.Count == 0) return;

            double centerX = 400;
            double centerY = 400;
            double radius = 370;

            double angleStep = 360.0 / entrants.Count;

            List<Color> colors = new() { Colors.RoyalBlue, Colors.Crimson, Colors.DarkGreen, Colors.Orange, Colors.Purple, Colors.DeepPink };

            for (int i = 0; i < entrants.Count; i++)
            {
                double startAngle = i * angleStep;
                Color color = colors[i % colors.Count];
                SolidColorBrush brush = new SolidColorBrush(color);

                // Segment
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

                Path segment = new Path { Data = geo, Fill = brush };
                RotatableCanvas.Children.Add(segment);
                _segmentPaths.Add(segment);

                // Text
                string name = entrants[i];
                double textAngle = startAngle + angleStep / 2;
                double textRadius = radius * 0.75;

                TextBlock tb = new TextBlock
                {
                    Text = name,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = Math.Max(10, 36 - entrants.Count / 3)
                };

                double rotation = (textAngle > 90 && textAngle < 270) ? 180 : 0;
                tb.RenderTransformOrigin = new Point(0.5, 0.5);
                tb.RenderTransform = new RotateTransform(rotation);

                tb.Measure(new Size(300, 100));
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