using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

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
            Width = 650;
            Height = 715;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            StackPanel panel = new StackPanel { Margin = new Thickness(15) };

            TextBlock header = new TextBlock
            {
                Text = "Type any part of the item name (case-insensitive)\n" +
                       "Results sorted smartly: exact match → starts with → earliest occurrence\n" +
                       "Use arrow keys + Enter, or double-click to select",
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };

            TextBox searchBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                FontSize = 16
            };
            searchBox.TextChanged += (s, e) =>
            {
                string query = searchBox.Text.Trim();
                var results = Gw2ItemDatabase.Search(query);
                _resultsList.ItemsSource = results;

                if (results.Count > 0)
                    _resultsList.SelectedIndex = 0;
            };

            _resultsList = new ListBox { Height = 500 };
            _resultsList.MouseDoubleClick += (s, e) => SelectCurrent();
            _resultsList.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                    SelectCurrent();
            };

            // DataTemplate: Name (bold) - Rarity (orange) - ID (gray)
            DataTemplate template = new DataTemplate();
            FrameworkElementFactory stack = new FrameworkElementFactory(typeof(StackPanel));
            stack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            FrameworkElementFactory nameBlock = new FrameworkElementFactory(typeof(TextBlock));
            nameBlock.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            nameBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            nameBlock.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 20, 0));
            nameBlock.SetValue(TextBlock.MinWidthProperty, 350.0);

            FrameworkElementFactory rarityBlock = new FrameworkElementFactory(typeof(TextBlock));
            rarityBlock.SetBinding(TextBlock.TextProperty, new Binding("Rarity"));
            rarityBlock.SetValue(TextBlock.ForegroundProperty, Brushes.Orange);
            rarityBlock.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 20, 0));

            FrameworkElementFactory idBlock = new FrameworkElementFactory(typeof(TextBlock));
            idBlock.SetBinding(TextBlock.TextProperty, new Binding("Id"));
            idBlock.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);

            stack.AppendChild(nameBlock);
            stack.AppendChild(rarityBlock);
            stack.AppendChild(idBlock);

            template.VisualTree = stack;
            _resultsList.ItemTemplate = template;

            Button selectBtn = new Button { Content = "Select", Width = 120, Height = 35, IsDefault = true };
            selectBtn.Click += (s, e) => SelectCurrent();

            Button cancelBtn = new Button { Content = "Cancel", Width = 120, Height = 35, IsCancel = true, Margin = new Thickness(10, 0, 0, 0) };
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
            buttons.Children.Add(cancelBtn);
            buttons.Children.Add(selectBtn);

            panel.Children.Add(header);
            panel.Children.Add(searchBox);
            panel.Children.Add(_resultsList);
            panel.Children.Add(buttons);

            Content = panel;

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
                MessageBox.Show("Please select a valid item from the list.");
            }
        }
    }
}