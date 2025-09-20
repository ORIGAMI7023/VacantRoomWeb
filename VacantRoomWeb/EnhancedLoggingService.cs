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
        private const int MaxRecentLogs = 1000;

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
    }
}