using System.Threading.Tasks;
using MudBlazor;

namespace SigmabotSync.ConfigWeb.Services
{
    public sealed class MudConfiguratorDialogs : IConfiguratorDialogs
    {
        private readonly IDialogService _dialogService;

        public MudConfiguratorDialogs(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public async Task ShowInfoAsync(string message, string title)
        {
            await _dialogService.ShowMessageBox(title, message, yesText: "Aceptar");
        }

        public async Task ShowWarningAsync(string message, string title)
        {
            await _dialogService.ShowMessageBox(title, message, yesText: "Aceptar");
        }

        public async Task<bool> ConfirmAsync(string message, string title)
        {
            var result = await _dialogService.ShowMessageBox(title, message, yesText: "Sí", cancelText: "No");
            return result == true;
        }
    }
}
