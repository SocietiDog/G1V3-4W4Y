using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Gw2Giveaway
{
    public class ItemSelectorDialog : Window
    {
        public ItemInfo? SelectedItem { get; private set; }
        public int SelectedId { get; private set; } = 0;

        private readonly ListBox _resultsList;

        public ItemSelectorDialog()
        {
            Title = "Search & Select Item";
            Width = 700;
            Height = 750;
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
                Padding = new Thickness(20),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.6 }
            };
            outer.MouseLeftButtonDown += (s, e) => { if (e.ChangedButton == MouseButton.Left) DragMove(); };

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid titleBar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            TextBlock titleText = new TextBlock
            {
                Text = "🔍 Search & Select Item",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.Gold),
                VerticalAlignment = VerticalAlignment.Center
            };
            Button closeBtn = CreateThemedButton("✕", false);
            closeBtn.Width = 40;
            closeBtn.Height = 40;
            closeBtn.FontSize = 12;
            closeBtn.HorizontalAlignment = HorizontalAlignment.Right;
            closeBtn.Click += (s, e) => { DialogResult = false; Close(); };
            titleBar.Children.Add(titleText);
            titleBar.Children.Add(closeBtn);
            Grid.SetRow(titleBar, 0);

            TextBlock header = new TextBlock
            {
                Text = "Type any part of the item name (case-insensitive)\n" +
                       "Results sorted smartly: exact match → starts with → earliest occurrence\n" +
                       "Use arrow keys + Enter, or double-click to select",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(header, 1);

            TextBox searchBox = new TextBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AA000000")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEFDFDF")),
                CaretBrush = Brushes.White,
                FontSize = 16,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(searchBox, 2);

            _resultsList = new ListBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CC000000")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55FFFFFF")),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var itemContainerStyle = new Style(typeof(ListBoxItem));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.White));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(8, 6, 8, 6)));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF))));
            var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xD7, 0x00))));
            itemContainerStyle.Triggers.Add(hoverTrigger);
            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xD7, 0x00))));
            selectedTrigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.White));
            itemContainerStyle.Triggers.Add(selectedTrigger);
            _resultsList.ItemContainerStyle = itemContainerStyle;

            _resultsList.MouseDoubleClick += (s, e) => SelectCurrent();
            _resultsList.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                    SelectCurrent();
            };
            Grid.SetRow(_resultsList, 3);

            searchBox.TextChanged += (s, e) =>
            {
                string query = searchBox.Text.Trim();
                var results = Gw2ItemDatabase.Search(query);
                _resultsList.ItemsSource = results;

                if (results.Count > 0)
                    _resultsList.SelectedIndex = 0;
            };

            DataTemplate template = new DataTemplate();
            FrameworkElementFactory stack = new FrameworkElementFactory(typeof(StackPanel));
            stack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            FrameworkElementFactory nameBlock = new FrameworkElementFactory(typeof(TextBlock));
            nameBlock.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            nameBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            nameBlock.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3EA")));
            nameBlock.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 20, 0));
            nameBlock.SetValue(TextBlock.MinWidthProperty, 350.0);

            FrameworkElementFactory rarityBlock = new FrameworkElementFactory(typeof(TextBlock));
            rarityBlock.SetBinding(TextBlock.TextProperty, new Binding("Rarity"));
            rarityBlock.SetValue(TextBlock.ForegroundProperty, Brushes.Orange);
            rarityBlock.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 20, 0));

            FrameworkElementFactory idBlock = new FrameworkElementFactory(typeof(TextBlock));
            idBlock.SetBinding(TextBlock.TextProperty, new Binding("Id"));
            idBlock.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)));

            stack.AppendChild(nameBlock);
            stack.AppendChild(rarityBlock);
            stack.AppendChild(idBlock);

            template.VisualTree = stack;
            _resultsList.ItemTemplate = template;

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Button cancelBtn = CreateThemedButton("Cancel", false);
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            Button selectBtn = CreateThemedButton("Select", true);
            selectBtn.Click += (s, e) => SelectCurrent();

            buttons.Children.Add(cancelBtn);
            buttons.Children.Add(selectBtn);
            Grid.SetRow(buttons, 4);

            mainGrid.Children.Add(titleBar);
            mainGrid.Children.Add(header);
            mainGrid.Children.Add(searchBox);
            mainGrid.Children.Add(_resultsList);
            mainGrid.Children.Add(buttons);

            outer.Child = mainGrid;
            Content = outer;

            Loaded += (s, e) =>
            {
                searchBox.Focus();
                searchBox.SelectAll();
            };
        }

        private void SelectCurrent()
        {
            if (_resultsList.SelectedItem is SearchResult sr && sr.Id > 0)
            {
                SelectedItem = Gw2ItemDatabase.Items[sr.Id];
                SelectedId = sr.Id;
                DialogResult = true;
                Close();
            }
            else
            {
                DialogService.ShowInfo("Please select a valid item from the list.");
            }
        }

        private static Button CreateThemedButton(string text, bool isDefault)
        {
            Button btn = new Button
            {
                Content = text,
                Width = 120,
                Height = 38,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3EA")),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 0, 0),
                IsDefault = isDefault,
                IsCancel = !isDefault
            };

            var tmpl = new ControlTemplate(typeof(Button));
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
            tmpl.VisualTree = border;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3F3EA"))));
            hover.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.Black));
            tmpl.Triggers.Add(hover);

            btn.Template = tmpl;
            return btn;
        }
    }
}