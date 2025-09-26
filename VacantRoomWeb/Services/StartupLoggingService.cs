// Services/StartupLoggingService.cs
namespace VacantRoomWeb.Services
{
    public interface IStartupLoggingService
    {
        void RecordStart();
        DateTime GetApplicationStartTime();
        TimeSpan GetUptime();
        string GetDetailedUptime();
        StartupInfo GetStartupInfo();
    }

    public class StartupInfo
    {
        public string StartTime { get; set; } = "";
        public string Uptime { get; set; } = "";
        public string TotalHours { get; set; } = "";
        public string Environment { get; set; } = "";
        public string MachineName { get; set; } = "";
        public string ProcessorCount { get; set; } = "";
        public string WorkingSet { get; set; } = "";
    }

    public class StartupLoggingService : IStartupLoggingService
    {
        private readonly DateTime _startTime;
        private readonly ILogger<StartupLoggingService> _logger;

        public StartupLoggingService(ILogger<StartupLoggingService> logger = null)
        {
            _startTime = DateTime.Now;
            _logger = logger;
        }

        public void RecordStart()
        {
            _logger?.LogInformation("Application started at {StartTime}", _startTime);
        }

        public DateTime GetApplicationStartTime()
        {
            return _startTime;
        }

        public TimeSpan GetUptime()
        {
            return DateTime.Now - _startTime;
        }

        public string GetDetailedUptime()
        {
            var uptime = GetUptime();
            return $"{uptime.Days}天 {uptime.Hours}小时 {uptime.Minutes}分钟 {uptime.Seconds}秒";
        }

        public StartupInfo GetStartupInfo()
        {
            var uptime = GetUptime();

            return new StartupInfo
            {
                StartTime = _startTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Uptime = GetDetailedUptime(),
                TotalHours = $"{uptime.TotalHours:F1}",
                Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                MachineName = System.Environment.MachineName,
                ProcessorCount = System.Environment.ProcessorCount.ToString(),
                WorkingSet = $"{System.Environment.WorkingSet / 1024 / 1024:F0} MB"
            };
        }
    }
}