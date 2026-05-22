using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Gw2Giveaway
{
    public class DisclaimerDialog : Window
    {
        public bool Accepted { get; private set; }

        public DisclaimerDialog(string title, string disclaimerText, bool requireAcceptance)
        {
            Title = title;
            Width = 760;
            Height = 620;
            MinWidth = 680;
            MinHeight = 520;
            ResizeMode = ResizeMode.CanResize;
            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            Background = Brushes.Transparent;
            DialogService.ConfigureDialogWindow(this);

            Border outer = new Border
            {
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEFDFDF")),
                BorderThickness = new Thickness(3),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EE1a1a2e")),
                Padding = new Thickness(24),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.6 }
            };
            outer.MouseLeftButtonDown += (s, e) => { if (e.ChangedButton == MouseButton.Left) DragMove(); };

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock header = new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3EA")),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };

            TextBlock text = new TextBlock
            {
                Text = disclaimerText,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3EA")),
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                LineHeight = 24
            };

            ScrollViewer scrollViewer = new ScrollViewer
            {
                Content = text,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0)
            };

            Border messageCard = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Child = scrollViewer,
                MinHeight = 340
            };

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };

            if (requireAcceptance)
            {
                Button decline = ChoiceDialogButtonFactory.Create("Decline");
                Button agree = ChoiceDialogButtonFactory.Create("I Agree", isDefault: true);

                decline.Click += (s, e) =>
                {
                    Accepted = false;
                    DialogResult = false;
                    Close();
                };

                agree.Click += (s, e) =>
                {
                    Accepted = true;
                    DialogResult = true;
                    Close();
                };

                buttons.Children.Add(decline);
                buttons.Children.Add(agree);
            }
            else
            {
                Button close = ChoiceDialogButtonFactory.Create("Close", isDefault: true);
                close.Click += (s, e) =>
                {
                    Accepted = true;
                    DialogResult = true;
                    Close();
                };

                buttons.Children.Add(close);
            }

            Grid.SetRow(header, 0);
            Grid.SetRow(messageCard, 1);
            Grid.SetRow(buttons, 2);
            grid.Children.Add(header);
            grid.Children.Add(messageCard);
            grid.Children.Add(buttons);

            outer.Child = grid;
            Content = outer;
        }
    }
}
