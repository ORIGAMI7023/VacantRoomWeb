namespace VacantRoomWeb.Services
{
    public class NotificationService
    {
        public event Action? LogsUpdated;

        public void NotifyLogsUpdated()
        {
            LogsUpdated?.Invoke();
        }
    }
}