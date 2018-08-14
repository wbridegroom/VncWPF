using System.Windows.Controls;
using System.Windows.Input;

namespace VncWpf
{
    /// <summary>
    /// Interaction logic for VncControl.xaml
    /// </summary>
    public partial class VncControl : UserControl
    {
        public VncControl()
        {
            InitializeComponent();
        }

        public VncViewModel ViewModel => DataContext as VncViewModel;

        private void KeyPressed(object sender, KeyEventArgs e)
        {
            if (ViewModel == null || !VncFrame.IsMouseOver && !Desktop.IsMouseOver) return;
            ViewModel.ManageKeyDownAndKeyUp(e, true);
            if (e.Handled) return;
            OnKeyDown(e);
        }

        private void KeyReleased(object sender, KeyEventArgs e)
        {
            if (ViewModel == null || !VncFrame.IsMouseOver && !Desktop.IsMouseOver) return;
            ViewModel.ManageKeyDownAndKeyUp(e, false);
            if (e.Handled) return;
            OnKeyDown(e);
        }

        private void MouseEntered(object sender, MouseEventArgs e)
        {
            if (ViewModel == null || ViewModel.ViewOnly) return;
            VncFrame.Focus();
            ViewModel.DisableSystemKeys();
        }

        private void MouseLeft(object sender, MouseEventArgs e)
        {
            ViewModel?.EnableSystemKeys();
        }

        private void MouseMoved(object sender, MouseEventArgs e)
        {
            ViewModel?.MouseEvent(e.LeftButton, e.MiddleButton, e.RightButton, 0, e.GetPosition(Desktop));
        }

        private void MouseButtonEvent(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel == null) return;
            ViewModel.MouseEvent(e.LeftButton, e.MiddleButton, e.RightButton, 0, e.GetPosition(Desktop));
            e.Handled = true;
        }

        private void MouseWheelChanged(object sender, MouseWheelEventArgs e)
        {
            ViewModel?.MouseEvent(e.LeftButton, e.MiddleButton, e.RightButton, e.Delta, e.GetPosition(Desktop));
        }
    }
}
