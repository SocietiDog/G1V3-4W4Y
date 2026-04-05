using System.Windows;
using System.Windows.Input;

namespace Gw2Giveaway
{
    public partial class TriviaOverlayWindow : Window
    {
        public TriviaOverlayWindow(TriviaViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            // Only drag-move if the event wasn't already handled (e.g., by the ResizeGrip)
            if (e.LeftButton == MouseButtonState.Pressed && !e.Handled)
            {
                this.DragMove();
            }
        }
    }
}