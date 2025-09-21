using System.Collections.Concurrent;

namespace VacantRoomWeb
{
    public class SecurityEvent
    {
        public DateTime Timestamp { get; set; }
        public string IP { get; set; } = "";
        public string EventType { get; set; } = "";
        public string Details { get; set; } = "";
    }

    public class SecurityService
    {
        private readonly EnhancedLoggingService _logger;
        private readonly IEmailService _emailService;

        // IP tracking for rate limiting
        private readonly ConcurrentDictionary<string, List<DateTime>> _ipRequests = new();
        private readonly ConcurrentDictionary<string, List<DateTime>> _loginAttempts = new();
        private readonly ConcurrentDictionary<string, DateTime> _bannedIPs = new();

        // System-wide security tracking
        private readonly List<DateTime> _recentBreachAttempts = new();
        private DateTime? _adminPanelLockdownUntil = null;
        private DateTime _lastEmailSent = DateTime.MinValue;

        private readonly object _lockObject = new();

        public SecurityService(EnhancedLoggingService logger, IEmailService emailService)
        {
            _logger = logger;
            _emailService = emailService;
        }

        public bool IsIPBanned(string ip)
        {
            if (_bannedIPs.TryGetValue(ip, out var banTime))
            {
                if (DateTime.Now < banTime)
                {
                    return true;
                }
                else
                {
                    _bannedIPs.TryRemove(ip, out _);
                }
            }
            return false;
        }

        public bool IsAdminPanelLocked()
        {
            if (_adminPanelLockdownUntil.HasValue && DateTime.Now < _adminPanelLockdownUntil.Value)
            {
                return true;
            }
            else
            {
                _adminPanelLockdownUntil = null;
                return false;
            }
        }

        public bool CheckRateLimit(string ip, string userAgent = "")
        {
            lock (_lockObject)
            {
                var now = DateTime.Now;
                var oneMinuteAgo = now.AddMinutes(-1);

                // Clean old requests
                _ipRequests.AddOrUpdate(ip, new List<DateTime> { now }, (key, list) =>
                {
                    list.RemoveAll(time => time < oneMinuteAgo);
                    list.Add(now);
                    return list;
                });

                var requestCount = _ipRequests[ip].Count;

                // 优化的DDoS检测：更宽松的阈值
                // 考虑到Blazor应用的特点，提高到120次/分钟
                if (requestCount > 120)
                {
                    BanIP(ip, TimeSpan.FromMinutes(15), "DDoS_DETECTED"); // 减少到15分钟
                    _logger.LogAccess(ip, "SECURITY_DDOS_DETECTED", $"Requests in 1min: {requestCount}", userAgent);

                    TriggerEmailAlert("DDoS Attack Detected", $"IP {ip} made {requestCount} requests in 1 minute and has been banned for 15 minutes.");

                    return false;
                }

                // 警告阈值：80次/分钟时记录但不封禁
                if (requestCount > 80)
                {
                    _logger.LogAccess(ip, "SECURITY_HIGH_REQUEST_RATE", $"High request rate: {requestCount}/min", userAgent);
                }

                return true;
            }
        }

        public bool CheckLoginAttempt(string ip, bool successful, string userAgent = "")
        {
            lock (_lockObject)
            {
                var now = DateTime.Now;
                var fiveMinutesAgo = now.AddMinutes(-5); // 扩展到5分钟窗口
                var tenMinutesAgo = now.AddMinutes(-10);

                if (successful)
                {
                    // Clear failed attempts on successful login
                    _loginAttempts.TryRemove(ip, out _);
                    _logger.LogAccess(ip, "ADMIN_LOGIN_SUCCESS", "", userAgent);
                    return true;
                }

                // Track failed attempts (使用5分钟窗口而不是1分钟)
                _loginAttempts.AddOrUpdate(ip, new List<DateTime> { now }, (key, list) =>
                {
                    list.RemoveAll(time => time < fiveMinutesAgo);
                    list.Add(now);
                    return list;
                });

                var failedCount = _loginAttempts[ip].Count;

                // 优化的暴力破解检测：5分钟内8次失败才封禁
                if (failedCount >= 8)
                {
                    BanIP(ip, TimeSpan.FromMinutes(30), "BRUTE_FORCE_DETECTED"); // 减少到30分钟
                    _logger.LogAccess(ip, "SECURITY_BRUTE_FORCE_DETECTED", $"Failed login attempts: {failedCount} in 5min", userAgent);

                    // Track for system-wide lockdown
                    _recentBreachAttempts.RemoveAll(time => time < tenMinutesAgo);
                    _recentBreachAttempts.Add(now);

                    TriggerEmailAlert("Brute Force Attack Detected", $"IP {ip} attempted {failedCount} failed logins in 5 minutes and has been banned for 30 minutes.");

                    // 系统锁定：10分钟内5个不同IP的暴力破解
                    var recentUniqueAttackers = _loginAttempts
                        .Where(kvp => kvp.Value.Any(time => time > tenMinutesAgo))
                        .Count();

                    if (recentUniqueAttackers >= 5)
                    {
                        _adminPanelLockdownUntil = now.AddMinutes(15); // 减少到15分钟
                        _logger.LogAccess(ip, "SECURITY_ADMIN_LOCKDOWN", $"Admin panel locked for 15 minutes due to {recentUniqueAttackers} attackers", userAgent);

                        TriggerEmailAlert("CRITICAL: Admin Panel Locked",
                            $"Admin panel has been locked for 15 minutes due to {recentUniqueAttackers} different IPs attempting brute force attacks. Latest attacker: {ip}");
                    }

                    return false;
                }

                // 警告阈值：5分钟内5次失败时记录警告
                if (failedCount >= 5)
                {
                    _logger.LogAccess(ip, "SECURITY_LOGIN_WARNING", $"Multiple failed attempts: {failedCount}/8 in 5min", userAgent);
                }

                _logger.LogAccess(ip, "ADMIN_LOGIN_FAILED", $"Attempt {failedCount}/8 in 5min", userAgent);
                return true;
            }
        }

        private void BanIP(string ip, TimeSpan duration, string reason)
        {
            var banUntil = DateTime.Now.Add(duration);
            _bannedIPs.AddOrUpdate(ip, banUntil, (key, oldTime) => banUntil);

            _logger.LogAccess(ip, "SECURITY_IP_BANNED", $"Reason: {reason}, Duration: {duration.TotalMinutes} minutes");
        }

        private void TriggerEmailAlert(string subject, string message)
        {
            var now = DateTime.Now;

            // Rate limit emails: max 1 every 15 minutes (更频繁的通知)
            if (now.Subtract(_lastEmailSent).TotalMinutes >= 15)
            {
                _lastEmailSent = now;
                _emailService.SendSecurityAlert(subject, message);
            }
        }

        public List<SecurityEvent> GetRecentSecurityEvents(int count = 50)
        {
            var events = new List<SecurityEvent>();
            var recentLogs = _logger.GetRecentLogs(count * 2);

            foreach (var log in recentLogs.Where(l => l.Action.StartsWith("SECURITY_") || l.Action.Contains("LOGIN")))
            {
                events.Add(new SecurityEvent
                {
                    Timestamp = log.Timestamp,
                    IP = log.IP,
                    EventType = log.Action,
                    Details = log.Details
                });
            }

            return events.OrderByDescending(e => e.Timestamp).Take(count).ToList();
        }

        public Dictionary<string, object> GetSecurityStats()
        {
            lock (_lockObject)
            {
                var now = DateTime.Now;
                var oneDayAgo = now.AddDays(-1);

                return new Dictionary<string, object>
                {
                    ["TotalBannedIPs"] = _bannedIPs.Count(kvp => kvp.Value > now),
                    ["AdminPanelLocked"] = IsAdminPanelLocked(),
                    ["LockdownUntil"] = _adminPanelLockdownUntil?.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["RecentBreachAttempts"] = _recentBreachAttempts.Count(t => t > oneDayAgo),
                    ["ActiveIPTracking"] = _ipRequests.Count
                };
            }
        }

        // 新增：手动解封IP的方法
        public bool UnbanIP(string ip)
        {
            return _bannedIPs.TryRemove(ip, out _);
        }

        // 新增：获取当前被封禁的IP列表
        public List<(string IP, DateTime BanUntil, TimeSpan Remaining)> GetBannedIPs()
        {
            var now = DateTime.Now;
            return _bannedIPs
                .Where(kvp => kvp.Value > now)
                .Select(kvp => (kvp.Key, kvp.Value, kvp.Value - now))
                .OrderBy(item => item.Item2)
                .ToList();
        }

        // 新增：清除过期的封禁记录
        public void CleanupExpiredBans()
        {
            var now = DateTime.Now;
            var expiredIPs = _bannedIPs
                .Where(kvp => kvp.Value <= now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var ip in expiredIPs)
            {
                _bannedIPs.TryRemove(ip, out _);
            }
        }
    }

    // Email service interface (placeholder for future implementation)
    public interface IEmailService
    {
        void SendSecurityAlert(string subject, string message);
    }

    public class EmailService : IEmailService
    {
        private readonly EnhancedLoggingService _logger;

        public EmailService(EnhancedLoggingService logger)
        {
            _logger = logger;
        }

        public void SendSecurityAlert(string subject, string message)
        {
            // Placeholder implementation - log the email attempt
            _logger.LogAccess("SYSTEM", "EMAIL_ALERT_TRIGGERED", $"Subject: {subject}, Message: {message}");

            // TODO: Implement actual email sending using SMTP
            // This will be implemented in the next phase
        }
    }
}