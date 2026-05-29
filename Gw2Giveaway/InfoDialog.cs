using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Gw2Giveaway
{
    public class InfoDialog : Window
    {
        public InfoDialog(string title, string message)
        {
            Title = title;
            Width = 460;
            Height = 220;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            Background = Brushes.Transparent;
            DialogService.ConfigureDialogWindow(this);

            Border outer = new Border
            {
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEFDFDF")),
                BorderThickness = new Thickness(3),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EE1C1008")),
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
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };

            TextBlock text = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3EA")),
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };

            Button ok = ChoiceDialogButtonFactory.Create("OK", isDefault: true);
            ok.Click += (s, e) => { DialogResult = true; Close(); };

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            buttons.Children.Add(ok);

            Border messageCard = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Child = text
            };

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

    internal static class ChoiceDialogButtonFactory
    {
        public static Button Create(string text, bool isDefault = false)
        {
            Button btn = new Button
            {
                Content = text,
                MinWidth = 90,
                Height = 36,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3EA")),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 0, 0),
                IsDefault = isDefault
            };

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            border.SetValue(Border.PaddingProperty, new Thickness(12, 6, 12, 6));
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(2));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3EA"))));
            hoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.Black));
            template.Triggers.Add(hoverTrigger);

            btn.Template = template;
            return btn;
        }
    }
}
