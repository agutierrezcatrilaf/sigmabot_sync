using System.Threading.Tasks;

namespace SigmabotSync.ConfigWeb.Services
{
    public interface IConfiguratorDialogs
    {
        Task ShowInfoAsync(string message, string title);

        Task ShowWarningAsync(string message, string title);

        Task<bool> ConfirmAsync(string message, string title);
    }
}
