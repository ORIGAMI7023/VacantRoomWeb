// Services/IEmailService.cs
namespace VacantRoomWeb.Services
{
    public interface IEmailService
    {
        Task<bool> SendSecurityAlertAsync(string subject, string message, string ipAddress = null);
        Task<bool> SendSystemNotificationAsync(string subject, string message);
        Task<bool> TestEmailServiceAsync();
        void SendSecurityAlert(string subject, string message);
        void SendSystemNotification(string subject, string message);
    }
}