// Services/FileBasedLoggingService.cs
using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Text;
using VacantRoomWeb.Models;

namespace VacantRoomWeb.Services
{
    public class FileBasedLoggingService : IDisposable
    {
        private readonly string _logDirectory;
        private readonly object _fileLock = new();
        private readonly ILogger<FileBasedLoggingService> _logger;

        // 缓存今日日志文件路径，避免重复计算
        private string _currentLogFilePath;
        private DateTime _currentLogDate;

        public FileBasedLoggingService(ILogger<FileBasedLoggingService> logger = null)
        {
            _logger = logger;
            _logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(_logDirectory);

            // 初始化当前日志文件路径
            UpdateCurrentLogFilePath();
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

            WriteToFile(logEntry);
        }

        private void WriteToFile(LogEntry entry)
        {
            lock (_fileLock)
            {
                try
                {
                    // 检查是否需要切换到新的日志文件（跨日期）
                    if (DateTime.Today != _currentLogDate)
                    {
                        UpdateCurrentLogFilePath();
                    }

                    var logLine = FormatLogEntry(entry);

                    // 使用追加模式写入文件
                    File.AppendAllText(_currentLogFilePath, logLine + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    // 静默处理文件写入错误，避免影响主业务
                    _logger?.LogError(ex, "写入日志文件失败: {FilePath}", _currentLogFilePath);
                }
            }
        }

        private void UpdateCurrentLogFilePath()
        {
            _currentLogDate = DateTime.Today;
            var fileName = $"access-{_currentLogDate:yyyy-MM-dd}.log";
            _currentLogFilePath = Path.Combine(_logDirectory, fileName);
        }

        private string FormatLogEntry(LogEntry entry)
        {
            return $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] IP:{entry.IP} ACTION:{entry.Action} PATH:{entry.RequestPath} DETAILS:{entry.Details} UA:{entry.UserAgent}";
        }

        public List<LogEntry> GetRecentLogs(int count = 100)
        {
            var logs = new List<LogEntry>();
            var today = DateTime.Today;

            // 从今天开始，向前查找日志文件
            for (int dayOffset = 0; dayOffset <= 7 && logs.Count < count; dayOffset++)
            {
                var date = today.AddDays(-dayOffset);
                var filePath = GetLogFilePath(date);

                if (File.Exists(filePath))
                {
                    var fileLogs = ReadLogsFromFile(filePath, count - logs.Count, true);
                    logs.AddRange(fileLogs);
                }
            }

            return logs.OrderByDescending(l => l.Timestamp).Take(count).ToList();
        }

        public List<LogEntry> GetLogsByDate(DateTime date)
        {
            var filePath = GetLogFilePath(date);

            if (!File.Exists(filePath))
                return new List<LogEntry>();

            return ReadLogsFromFile(filePath, int.MaxValue, false);
        }

        private List<LogEntry> ReadLogsFromFile(string filePath, int maxCount, bool fromEnd)
        {
            var logs = new List<LogEntry>();

            lock (_fileLock)
            {
                try
                {
                    var lines = File.ReadAllLines(filePath, Encoding.UTF8);

                    if (fromEnd)
                    {
                        // 从文件末尾开始读取
                        for (int i = lines.Length - 1; i >= 0 && logs.Count < maxCount; i--)
                        {
                            if (TryParseLogLine(lines[i], out var logEntry))
                            {
                                logs.Add(logEntry);
                            }
                        }
                    }
                    else
                    {
                        // 从文件开头读取
                        foreach (var line in lines)
                        {
                            if (TryParseLogLine(line, out var logEntry))
                            {
                                logs.Add(logEntry);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "读取日志文件失败: {FilePath}", filePath);
                }
            }

            return logs;
        }

        private bool TryParseLogLine(string line, out LogEntry logEntry)
        {
            logEntry = new LogEntry();

            try
            {
                if (string.IsNullOrEmpty(line) || !line.StartsWith("["))
                    return false;

                // 解析时间戳
                var timestampEnd = line.IndexOf(']');
                if (timestampEnd == -1) return false;

                var timestampStr = line.Substring(1, timestampEnd - 1);
                if (!DateTime.TryParseExact(timestampStr, "yyyy-MM-dd HH:mm:ss.fff", null, System.Globalization.DateTimeStyles.None, out var timestamp))
                    return false;

                logEntry.Timestamp = timestamp;

                // 解析其余字段
                var remainingLine = line.Substring(timestampEnd + 1).Trim();
                var parts = remainingLine.Split(new[] { " IP:", " ACTION:", " PATH:", " DETAILS:", " UA:" }, StringSplitOptions.None);

                if (parts.Length >= 5)
                {
                    logEntry.IP = parts[1];
                    logEntry.Action = parts[2];
                    logEntry.RequestPath = parts[3];
                    logEntry.Details = parts[4];

                    if (parts.Length > 5)
                    {
                        logEntry.UserAgent = parts[5];
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "解析日志行失败: {Line}", line);
            }

            return false;
        }

        private string GetLogFilePath(DateTime date)
        {
            var fileName = $"access-{date:yyyy-MM-dd}.log";
            return Path.Combine(_logDirectory, fileName);
        }

        public List<string> GetAvailableLogDates()
        {
            try
            {
                return Directory.GetFiles(_logDirectory, "access-*.log")
                    .Select(f => Path.GetFileName(f))
                    .Select(f => f.Replace("access-", "").Replace(".log", ""))
                    .Where(dateStr => DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _))
                    .OrderByDescending(d => d)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取可用日志日期失败");
                return new List<string>();
            }
        }

        public void ClearRecentLogs()
        {
            // 对于文件系统，这个方法可以清空今日的日志文件
            lock (_fileLock)
            {
                try
                {
                    if (File.Exists(_currentLogFilePath))
                    {
                        File.WriteAllText(_currentLogFilePath, "", Encoding.UTF8);
                        _logger?.LogInformation("已清空今日日志文件: {FilePath}", _currentLogFilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "清空日志文件失败: {FilePath}", _currentLogFilePath);
                }
            }
        }

        public Dictionary<string, object> GetLogStats()
        {
            var stats = new Dictionary<string, object>();
            var today = DateTime.Today;

            try
            {
                // 今日日志统计
                var todayLogs = GetLogsByDate(today);
                var todayActionGroups = todayLogs.GroupBy(l => l.Action).ToDictionary(g => g.Key, g => g.Count());
                var todayIpCount = todayLogs.Select(l => l.IP).Distinct().Count();

                stats["TodayLogs"] = todayLogs.Count;
                stats["TodayUniqueIPs"] = todayIpCount;
                stats["TodaySecurityEvents"] = todayActionGroups.Where(kvp => kvp.Key.StartsWith("SECURITY_")).Sum(kvp => kvp.Value);
                stats["TodayAdminEvents"] = todayActionGroups.Where(kvp => kvp.Key.Contains("ADMIN")).Sum(kvp => kvp.Value);

                // 内存日志统计（始终为0，因为不再使用内存）
                stats["MemoryLogs"] = 0;
                stats["MemoryUniqueIPs"] = 0;
                stats["MemorySecurityEvents"] = 0;
                stats["MemoryAdminEvents"] = 0;

                // 今日日志文件大小
                var todayFilePath = GetLogFilePath(today);
                if (File.Exists(todayFilePath))
                {
                    var fileInfo = new FileInfo(todayFilePath);
                    stats["TodayLogFileSize"] = $"{fileInfo.Length / 1024.0:F1} KB";
                }
                else
                {
                    stats["TodayLogFileSize"] = "0 KB";
                }

                // 调试信息
                stats["DebugInfo"] = $"今日文件日志: {todayLogs.Count}";
                stats["DebugLogDirectory"] = _logDirectory;
                stats["DebugCurrentTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                stats["DebugTodayDate"] = today.ToString("yyyy-MM-dd");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取日志统计失败");

                // 返回基础统计
                stats["TodayLogs"] = 0;
                stats["MemoryLogs"] = 0;
                stats["TodayLogFileSize"] = "0 KB";
            }

            return stats;
        }

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
                            _logger?.LogInformation("删除过期日志文件: {File}", file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清理旧日志文件失败");
            }
        }

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
                        var lineCount = CountLinesInFile(file);
                        var size = $"{fileInfo.Length / 1024.0:F1} KB";

                        result.Add((dateStr, lineCount, size));
                    }
                }

                return result.OrderByDescending(r => r.Date).ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取日志文件信息失败");
                return result;
            }
        }

        private int CountLinesInFile(string filePath)
        {
            try
            {
                return File.ReadAllLines(filePath).Length;
            }
            catch
            {
                return 0;
            }
        }

        public void Dispose()
        {
            // 文件系统不需要特殊的释放操作
        }
    }
}