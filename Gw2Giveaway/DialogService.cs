using System.Linq;
using System.Windows;

namespace Gw2Giveaway
{
    internal static class DialogService
    {
        public static void ConfigureDialogWindow(Window dialog)
        {
            Window? owner = GetBestOwner();
            if (owner != null && !ReferenceEquals(owner, dialog))
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialog.ShowInTaskbar = false;
            dialog.Topmost = true;
        }

        public static void ShowInfo(string message, string title = "Gw2Giveaway")
        {
            var dialog = new InfoDialog(title, message);
            dialog.ShowDialog();
        }

        public static MessageBoxResult ShowConfirm(string message, string title = "Confirm", string yesText = "Yes", string noText = "No", string cancelText = "Cancel")
        {
            var dialog = new ChoiceDialog(title, message, yesText, noText, cancelText);
            dialog.ShowDialog();
            return dialog.Result;
        }

        private static Window? GetBestOwner()
        {
            var windows = Application.Current?.Windows.OfType<Window>().Where(w => w.IsVisible).ToList();
            if (windows == null || windows.Count == 0)
                return Application.Current?.MainWindow;

            return windows.FirstOrDefault(w => w.IsActive)
                ?? windows.OfType<OverlayWindow>().FirstOrDefault()
                ?? windows.OfType<MainWindow>().FirstOrDefault()
                ?? Application.Current?.MainWindow;
        }
    }
}
