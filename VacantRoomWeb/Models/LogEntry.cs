// Models/LogEntry.cs
namespace VacantRoomWeb.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string IP { get; set; } = "";
        public string Action { get; set; } = "";
        public string Details { get; set; } = "";
        public string UserAgent { get; set; } = "";
        public string RequestPath { get; set; } = "";
    }
}