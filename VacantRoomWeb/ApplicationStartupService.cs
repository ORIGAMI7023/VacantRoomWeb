namespace VacantRoomWeb
{
    /// <summary>
    /// 跟踪应用程序实际启动时间的服务
    /// </summary>
    public class ApplicationStartupService
    {
        // 使用静态字段，但通过静态构造函数确保在应用启动时初始化
        private static readonly DateTime _applicationStartTime;

        static ApplicationStartupService()
        {
            _applicationStartTime = DateTime.Now;
        }

        public DateTime GetApplicationStartTime()
        {
            return _applicationStartTime;
        }

        public string GetUptime()
        {
            var uptime = DateTime.Now - _applicationStartTime;
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
        }

        public string GetDetailedUptime()
        {
            var uptime = DateTime.Now - _applicationStartTime;

            if (uptime.TotalDays >= 1)
            {
                return $"{uptime.Days}天 {uptime.Hours}小时 {uptime.Minutes}分钟";
            }
            else if (uptime.TotalHours >= 1)
            {
                return $"{uptime.Hours}小时 {uptime.Minutes}分钟";
            }
            else
            {
                return $"{uptime.Minutes}分钟 {uptime.Seconds}秒";
            }
        }

        /// <summary>
        /// 获取应用程序启动相关信息
        /// </summary>
        public Dictionary<string, string> GetStartupInfo()
        {
            var uptime = DateTime.Now - _applicationStartTime;

            return new Dictionary<string, string>
            {
                ["StartTime"] = _applicationStartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                ["Uptime"] = GetDetailedUptime(),
                ["TotalHours"] = uptime.TotalHours.ToString("F1"),
                ["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "未知",
                ["MachineName"] = Environment.MachineName,
                ["ProcessorCount"] = Environment.ProcessorCount.ToString(),
                ["WorkingSet"] = $"{Environment.WorkingSet / 1024 / 1024:F0} MB"
            };
        }
    }
}