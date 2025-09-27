// EmailNotificationSettings.cs
namespace VacantRoomWeb.Models
{
    public class EmailNotificationSettings
    {
        public bool EnableDDoSAlerts { get; set; }
        public bool EnableBruteForceAlerts { get; set; }
        public bool EnableSystemLockdownAlerts { get; set; }
        public bool EnableIPBanAlerts { get; set; }
    }
}