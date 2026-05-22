using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Gw2Giveaway
{
    public class InputDialog : Window
    {
        public string Result { get; private set; }

        public InputDialog(string prompt, string defaultText = "")
        {
            Title = "Input";
            Width = 400;
            Height = 200;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            Background = Brushes.Transparent;
            DialogService.ConfigureDialogWindow(this);

            // Outer border matching app theme
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

            TextBlock label = new TextBlock
            {
                Text = prompt,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3EA")),
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };

            TextBox textBox = new TextBox
            {
                Text = defaultText,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AA000000")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEFDFDF")),
                CaretBrush = Brushes.White,
                FontSize = 15,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 4, 0, 4),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };

            Button ok = CreateThemedButton("OK", true);
            Button cancel = CreateThemedButton("Cancel", false);

            ok.Click += (s, e) => { Result = textBox.Text; DialogResult = true; Close(); };
            cancel.Click += (s, e) => { DialogResult = false; Close(); };

            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);

            Grid.SetRow(label, 0);
            Grid.SetRow(textBox, 1);
            Grid.SetRow(buttons, 2);

            grid.Children.Add(label);
            grid.Children.Add(textBox);
            grid.Children.Add(buttons);

            outer.Child = grid;
            Content = outer;

            Loaded += (s, e) => { textBox.Focus(); textBox.SelectAll(); };
        }

        private static Button CreateThemedButton(string text, bool isDefault)
        {
            Button btn = new Button
            {
                Content = text,
                Width = 90,
                Height = 36,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3EA")),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 0, 0),
                IsDefault = isDefault,
                IsCancel = !isDefault
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