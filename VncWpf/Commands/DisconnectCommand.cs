namespace VncWpf.Commands
{
    public class DisconnectCommand : BaseCommand
    {
        private readonly VncViewModel viewModel;

        public DisconnectCommand(VncViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public override void Execute(object parameter)
        {
            viewModel.Client.Disconnect();
            viewModel.IsConnected = false;
            viewModel.Desktop.Source = null;
        }

        public override bool CanExecute(object parameter)
        {
            return viewModel.IsConnected;
        }
    }
}
