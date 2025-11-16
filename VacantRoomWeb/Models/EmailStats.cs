// Models/EmailStats.cs
namespace VacantRoomWeb.Models
{
    public class EmailStats
    {
        public int TotalSent { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public DateTime? LastSentTime { get; set; }
        public double SuccessRate => TotalSent > 0 ? (SuccessCount * 100.0 / TotalSent) : 0;
        public string LastSentTimeFormatted => LastSentTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未发送";
    }
}
