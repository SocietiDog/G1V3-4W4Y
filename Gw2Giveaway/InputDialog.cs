using System.Windows;
using System.Windows.Controls;

namespace Gw2Giveaway
{
    public class InputDialog : Window
    {
        public string Result { get; private set; }

        public InputDialog(string prompt, string defaultText = "")
        {
            Title = "Input";
            Width = 350;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            Grid grid = new Grid();
            grid.Margin = new Thickness(10);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock label = new TextBlock { Text = prompt };
            TextBox textBox = new TextBox { Text = defaultText, Margin = new Thickness(0, 10, 0, 10) };

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button ok = new Button { Content = "OK", Width = 80, Margin = new Thickness(5, 0, 0, 0), IsDefault = true };
            Button cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };

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

            Content = grid;
        }
    }
}