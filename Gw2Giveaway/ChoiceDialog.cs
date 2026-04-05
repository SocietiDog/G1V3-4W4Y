using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Gw2Giveaway
{
    public class ChoiceDialog : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        public ChoiceDialog(string title, string prompt, string yesText = "Yes", string noText = "No", string cancelText = "Cancel")
        {
            Title = title;
            Width = 520;
            Height = 260;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            Background = Brushes.Transparent;

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
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };

            TextBlock label = new TextBlock
            {
                Text = prompt,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3EA")),
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };

            StackPanel content = new StackPanel();
            content.Children.Add(header);
            content.Children.Add(label);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };

            Button yes = CreateThemedButton(yesText);
            Button no = CreateThemedButton(noText);
            Button cancel = CreateThemedButton(cancelText);

            yes.Click += (s, e) => { Result = MessageBoxResult.Yes; DialogResult = true; Close(); };
            no.Click += (s, e) => { Result = MessageBoxResult.No; DialogResult = true; Close(); };
            cancel.Click += (s, e) => { Result = MessageBoxResult.Cancel; DialogResult = false; Close(); };

            buttons.Children.Add(cancel);
            buttons.Children.Add(no);
            buttons.Children.Add(yes);

            Grid.SetRow(content, 0);
            Grid.SetRow(buttons, 2);

            grid.Children.Add(content);
            grid.Children.Add(buttons);

            outer.Child = grid;
            Content = outer;
        }

        private static Button CreateThemedButton(string text)
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
                Margin = new Thickness(6, 0, 0, 0)
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
