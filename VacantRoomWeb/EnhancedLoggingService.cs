using System.Collections.Concurrent;

namespace VacantRoomWeb
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

    public class EnhancedLoggingService
    {
        private readonly string _logDirectory;
        private readonly ConcurrentQueue<LogEntry> _recentLogs = new();
        private readonly object _fileLock = new();
        private const int MaxRecentLogs = 500;

        public EnhancedLoggingService()
        {
            _logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(_logDirectory);
        }

        public void LogAccess(string ip, string action, string details = "", string userAgent = "", string requestPath = "")
        {
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                IP = ip,
                Action = action,
                Details = details,
                UserAgent = userAgent,
                RequestPath = requestPath
            };

            // Add to memory queue
            _recentLogs.Enqueue(logEntry);

            // Keep only recent logs in memory
            while (_recentLogs.Count > MaxRecentLogs)
            {
                _recentLogs.TryDequeue(out _);
            }

            // Write to file (no console output for IIS)
            WriteToFile(logEntry);
        }

        private void WriteToFile(LogEntry entry)
        {
            try
            {
                var fileName = $"access-{DateTime.Now:yyyy-MM-dd}.log";
                var filePath = Path.Combine(_logDirectory, fileName);

                var logLine = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] IP:{entry.IP} ACTION:{entry.Action} PATH:{entry.RequestPath} DETAILS:{entry.Details} UA:{entry.UserAgent}";

                lock (_fileLock)
                {
                    File.AppendAllText(filePath, logLine + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail in production environment
            }
        }

        public List<LogEntry> GetRecentLogs(int count = 100)
        {
            return _recentLogs.TakeLast(count).ToList();
        }

        public List<LogEntry> GetLogsByDate(DateTime date)
        {
            var fileName = $"access-{date:yyyy-MM-dd}.log";
            var filePath = Path.Combine(_logDirectory, fileName);

            if (!File.Exists(filePath))
                return new List<LogEntry>();

            try
            {
                var lines = File.ReadAllLines(filePath);
                var logs = new List<LogEntry>();

                foreach (var line in lines)
                {
                    if (TryParseLogLine(line, out var logEntry))
                    {
                        logs.Add(logEntry);
                    }
                }

                return logs;
            }
            catch
            {
                return new List<LogEntry>();
            }
        }

        private bool TryParseLogLine(string line, out LogEntry logEntry)
        {
            logEntry = new LogEntry();

            try
            {
                var parts = line.Split(new[] { " IP:", " ACTION:", " PATH:", " DETAILS:", " UA:" }, StringSplitOptions.None);

                if (parts.Length >= 6)
                {
                    var timestampStr = parts[0].Trim('[', ']');
                    logEntry.Timestamp = DateTime.Parse(timestampStr);
                    logEntry.IP = parts[1];
                    logEntry.Action = parts[2];
                    logEntry.RequestPath = parts[3];
                    logEntry.Details = parts[4];
                    logEntry.UserAgent = parts[5];
                    return true;
                }
            }
            catch
            {
                // Ignore parse errors
            }

            return false;
        }

        public List<string> GetAvailableLogDates()
        {
            try
            {
                return Directory.GetFiles(_logDirectory, "access-*.log")
                    .Select(f => Path.GetFileName(f))
                    .Select(f => f.Replace("access-", "").Replace(".log", ""))
                    .OrderByDescending(d => d)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        // 清理内存日志
        public void ClearRecentLogs()
        {
            while (_recentLogs.TryDequeue(out _)) { }
        }

        // 获取日志统计信息 - 区分内存和当日文件
        public Dictionary<string, object> GetLogStats()
        {
            var memoryLogs = _recentLogs.ToList();
            var todayLogs = GetLogsByDate(DateTime.Today);

            var stats = new Dictionary<string, object>();

            // 内存日志统计
            var memoryActionGroups = memoryLogs.GroupBy(l => l.Action).ToDictionary(g => g.Key, g => g.Count());
            var memoryIpCount = memoryLogs.Select(l => l.IP).Distinct().Count();

            stats["MemoryLogs"] = memoryLogs.Count;
            stats["MemoryUniqueIPs"] = memoryIpCount;
            stats["MemorySecurityEvents"] = memoryActionGroups.Where(kvp => kvp.Key.StartsWith("SECURITY_")).Sum(kvp => kvp.Value);
            stats["MemoryAdminEvents"] = memoryActionGroups.Where(kvp => kvp.Key.Contains("ADMIN")).Sum(kvp => kvp.Value);

            // 当日文件日志统计
            var todayActionGroups = todayLogs.GroupBy(l => l.Action).ToDictionary(g => g.Key, g => g.Count());
            var todayIpCount = todayLogs.Select(l => l.IP).Distinct().Count();

            stats["TodayLogs"] = todayLogs.Count;
            stats["TodayUniqueIPs"] = todayIpCount;
            stats["TodaySecurityEvents"] = todayActionGroups.Where(kvp => kvp.Key.StartsWith("SECURITY_")).Sum(kvp => kvp.Value);
            stats["TodayAdminEvents"] = todayActionGroups.Where(kvp => kvp.Key.Contains("ADMIN")).Sum(kvp => kvp.Value);

            // 文件大小信息
            var todayFileName = $"access-{DateTime.Today:yyyy-MM-dd}.log";
            var todayFilePath = Path.Combine(_logDirectory, todayFileName);

            if (File.Exists(todayFilePath))
            {
                var fileInfo = new FileInfo(todayFilePath);
                stats["TodayLogFileSize"] = $"{fileInfo.Length / 1024.0:F1} KB";
            }
            else
            {
                stats["TodayLogFileSize"] = "0 KB";
            }

            return stats;
        }

        // 获取当日日志文件的行数（快速方法）
        public int GetTodayLogCount()
        {
            var fileName = $"access-{DateTime.Today:yyyy-MM-dd}.log";
            var filePath = Path.Combine(_logDirectory, fileName);

            if (!File.Exists(filePath))
                return 0;

            try
            {
                return File.ReadAllLines(filePath).Length;
            }
            catch
            {
                return 0;
            }
        }

        // 删除旧日志文件
        public void CleanupOldLogs(int daysToKeep = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(_logDirectory, "access-*.log");

                foreach (var file in logFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var dateStr = fileName.Replace("access-", "");

                    if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
                    {
                        if (fileDate < cutoffDate)
                        {
                            File.Delete(file);
                        }
                    }
                }
            }
            catch
            {
                // Silently fail
            }
        }

        // 获取日志文件信息
        public List<(string Date, int Count, string Size)> GetLogFileInfo()
        {
            var result = new List<(string Date, int Count, string Size)>();

            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "access-*.log");

                foreach (var file in logFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var dateStr = fileName.Replace("access-", "");

                    if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
                    {
                        var fileInfo = new FileInfo(file);
                        var lineCount = File.ReadAllLines(file).Length;
                        var size = $"{fileInfo.Length / 1024.0:F1} KB";

                        result.Add((dateStr, lineCount, size));
                    }
                }

                return result.OrderByDescending(r => r.Date).ToList();
            }
            catch
            {
                return result;
            }
        }
    }
}