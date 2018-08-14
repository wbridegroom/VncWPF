using System;

namespace VncWpf.Commands
{
    public class SendCtrlAltDelCommand : BaseCommand
    {
        private readonly VncViewModel viewModel;

        public SendCtrlAltDelCommand(VncViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public override void Execute(object parameter)
        {
            try
            {
                var client = viewModel.Client;
                client.KeyboardEvent(65507U, true);
                client.KeyboardEvent(65513U, true);
                client.KeyboardEvent(ushort.MaxValue, true);
                client.KeyboardEvent(ushort.MaxValue, false);
                client.KeyboardEvent(65513U, false);
                client.KeyboardEvent(65507U, false);
            }
            catch (Exception ex)
            {
                viewModel.ConnectionClosed("Error: " + ex.Message);
            }
        }

        public override bool CanExecute(object parameter)
        {
            return viewModel.IsConnected;
        }
    }
}
