// Services/StartupLoggingService.cs - 持久化版本
using System.Text.Json;

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
        private readonly string _startupFilePath;

        public StartupLoggingService(ILogger<StartupLoggingService> logger = null)
        {
            _logger = logger;
            _startupFilePath = Path.Combine(AppContext.BaseDirectory, "Logs", "startup_time.txt");

            // 确保日志目录存在
            var logDirectory = Path.GetDirectoryName(_startupFilePath);
            if (!string.IsNullOrEmpty(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // 优先从文件读取启动时间
            if (File.Exists(_startupFilePath))
            {
                try
                {
                    var savedTime = File.ReadAllText(_startupFilePath);
                    if (DateTime.TryParse(savedTime, out var parsedTime))
                    {
                        _startTime = parsedTime;
                        _logger?.LogInformation("从持久化文件读取启动时间: {StartTime} (已运行 {Uptime})",
                            _startTime, DateTime.Now - _startTime);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "无法读取启动时间文件");
                }
            }

            // 文件不存在 - 这是真正的首次启动或重新部署
            _startTime = DateTime.Now;
            SaveStartupTime();
            _logger?.LogInformation("检测到首次启动或重新部署，创建新的启动时间: {StartTime}", _startTime);
        }

        private void SaveStartupTime()
        {
            try
            {
                File.WriteAllText(_startupFilePath, _startTime.ToString("O"));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存启动时间失败");
            }
        }

        public void RecordStart()
        {
            var uptime = DateTime.Now - _startTime;
            _logger?.LogInformation("应用进程启动 - 系统初始启动时间: {StartTime}, 当前已运行: {Uptime}",
                _startTime, $"{uptime.Days}天{uptime.Hours}小时{uptime.Minutes}分钟");
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

        // 手动重置启动时间（可选功能）
        public void ResetStartupTime()
        {
            var newStartTime = DateTime.Now;
            File.WriteAllText(_startupFilePath, newStartTime.ToString("O"));
            _logger?.LogWarning("启动时间已手动重置为 {NewStartTime}", newStartTime);
        }

        // 删除持久化文件，下次启动会创建新的（可选功能）
        public void ClearPersistedStartupTime()
        {
            try
            {
                if (File.Exists(_startupFilePath))
                {
                    File.Delete(_startupFilePath);
                    _logger?.LogInformation("已删除持久化的启动时间文件");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除启动时间文件失败");
            }
        }
    }
}