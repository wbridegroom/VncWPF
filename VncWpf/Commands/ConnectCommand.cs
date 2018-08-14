using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VncWpf.Commands
{
    public class ConnectCommand : BaseCommand
    {
        private readonly VncViewModel viewModel;

        public ConnectCommand(VncViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public override bool CanExecute(object parameter)
        {
            return !viewModel.IsConnected;
        }

        public override void Execute(object parameter)
        {
            Connect();
        }

        private void Connect()
        {
            try
            {
                viewModel.Desktop = new Image { Stretch = Stretch.None };
                viewModel.Desktop.SizeChanged += SizeChanged;

                RenderOptions.SetBitmapScalingMode(viewModel.Desktop, BitmapScalingMode.HighQuality);
                RenderOptions.SetEdgeMode(viewModel.Desktop, EdgeMode.Aliased);

                var frameBuffer = viewModel.Client.Connect();
                if (frameBuffer != null)
                {
                    viewModel.Height = frameBuffer.Height;
                    viewModel.Width = frameBuffer.Width;
                    viewModel.DesktopBitmap = new WriteableBitmap(frameBuffer.Width, frameBuffer.Height, 96.0, 96.0, PixelFormats.Pbgra32, null);
                    viewModel.Desktop.Source = viewModel.DesktopBitmap;
                    viewModel.Client.BeginUpdates();

                    if (!viewModel.ViewOnly) viewModel.FocusedElement = "VncFrame";
                }
                viewModel.IsConnected = true;


                viewModel.Dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception)
            {
                viewModel.IsConnected = false;
            }
        }

        private void SizeChanged(object sender, SizeChangedEventArgs e)
        {
            viewModel.Height = viewModel.Desktop.ActualHeight;
            viewModel.Width = viewModel.Desktop.ActualWidth;
        }
    }
}
