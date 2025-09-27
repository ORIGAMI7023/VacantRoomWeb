namespace VacantRoomWeb.Services
{
    /// <summary>
    /// 兼容性适配器 - 保持现有代码的接口不变
    /// 内部委托给基于文件的 StartupLoggingService
    /// </summary>
    public class ApplicationStartupService
    {
        private readonly IStartupLoggingService _startupLoggingService;

        public ApplicationStartupService(IStartupLoggingService startupLoggingService)
        {
            _startupLoggingService = startupLoggingService;
        }

        public DateTime GetApplicationStartTime()
        {
            return _startupLoggingService.GetApplicationStartTime();
        }

        public string GetUptime()
        {
            var uptime = _startupLoggingService.GetUptime();
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
        }

        public string GetDetailedUptime()
        {
            return _startupLoggingService.GetDetailedUptime();
        }

        /// <summary>
        /// 获取应用程序启动相关信息
        /// </summary>
        public Dictionary<string, string> GetStartupInfo()
        {
            var startupInfo = _startupLoggingService.GetStartupInfo();

            return new Dictionary<string, string>
            {
                ["StartTime"] = startupInfo.StartTime,
                ["Uptime"] = startupInfo.Uptime,
                ["TotalHours"] = startupInfo.TotalHours,
                ["Environment"] = startupInfo.Environment,
                ["MachineName"] = startupInfo.MachineName,
                ["ProcessorCount"] = startupInfo.ProcessorCount,
                ["WorkingSet"] = startupInfo.WorkingSet
            };
        }
    }
}