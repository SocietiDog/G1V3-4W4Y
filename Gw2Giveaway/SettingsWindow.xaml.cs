using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Gw2Giveaway
{
    public partial class SettingsWindow : Window
    {
        private readonly TriviaViewModel _viewModel;
        private readonly MainWindow _mainWindow;

        public SettingsWindow(TriviaViewModel viewModel, MainWindow mainWindow)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _mainWindow = mainWindow;
            DataContext = _viewModel;

            // Manually initialize picker swatches with current colors (fixes black on open)
            SetPickerColor(BackgroundPicker, _viewModel.OverlayBackground);
            SetPickerColor(InnerBackgroundPicker, _viewModel.OverlayInnerBackground);
            SetPickerColor(BorderColorPicker, _viewModel.OverlayBorderColor);
            SetPickerColor(TitleColorPicker, _viewModel.TitleColor);
            SetPickerColor(HeaderColorPicker, _viewModel.HeaderColor);
            SetPickerColor(TextColorPicker, _viewModel.TextColor);
            SetPickerColor(AccentColorPicker, _viewModel.AccentColor);
            SetPickerColor(GoldColorPicker, _viewModel.GoldColor);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.SaveSettings();
            Close();
        }

        private void SetPickerColor(ColorPicker.PortableColorPicker picker, string hex)
        {
            try
            {
                picker.SelectedColor = (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                picker.SelectedColor = Colors.Black; // Fallback
            }
        }

        private void ColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ColorPicker.PortableColorPicker picker && picker.Tag is string propertyName)
            {
                Color selected = picker.SelectedColor;
                string hex = $"#{selected.A:X2}{selected.R:X2}{selected.G:X2}{selected.B:X2}";
                var prop = _viewModel.GetType().GetProperty(propertyName);
                prop?.SetValue(_viewModel, hex);
            }
        }

        private void ResetColors_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.OverlayBackground = "#DD000000";
            _viewModel.OverlayInnerBackground = "#EE000000";
            _viewModel.OverlayBorderColor = "#FFD700";
            _viewModel.TitleColor = "#FFD700";
            _viewModel.HeaderColor = "#FFCC00";
            _viewModel.TextColor = "#FFFFFF";
            _viewModel.AccentColor = "#00FFAA";
            _viewModel.GoldColor = "#FFFF00";

            // Update pickers after reset
            SetPickerColor(BackgroundPicker, _viewModel.OverlayBackground);
            SetPickerColor(InnerBackgroundPicker, _viewModel.OverlayInnerBackground);
            SetPickerColor(BorderColorPicker, _viewModel.OverlayBorderColor);
            SetPickerColor(TitleColorPicker, _viewModel.TitleColor);
            SetPickerColor(HeaderColorPicker, _viewModel.HeaderColor);
            SetPickerColor(TextColorPicker, _viewModel.TextColor);
            SetPickerColor(AccentColorPicker, _viewModel.AccentColor);
            SetPickerColor(GoldColorPicker, _viewModel.GoldColor);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.SaveSettings();
            Close();
        }

        
    }
}