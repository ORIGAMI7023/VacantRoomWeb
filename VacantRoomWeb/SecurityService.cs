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

                // DDoS detection: 30 requests per minute
                if (requestCount > 30)
                {
                    BanIP(ip, TimeSpan.FromHours(1), "DDoS_DETECTED");
                    _logger.LogAccess(ip, "SECURITY_DDOS_DETECTED", $"Requests in 1min: {requestCount}", userAgent);

                    TriggerEmailAlert("DDoS Attack Detected", $"IP {ip} made {requestCount} requests in 1 minute and has been banned.");

                    return false;
                }

                return true;
            }
        }

        public bool CheckLoginAttempt(string ip, bool successful, string userAgent = "")
        {
            lock (_lockObject)
            {
                var now = DateTime.Now;
                var oneMinuteAgo = now.AddMinutes(-1);
                var fiveMinutesAgo = now.AddMinutes(-5);

                if (successful)
                {
                    // Clear failed attempts on successful login
                    _loginAttempts.TryRemove(ip, out _);
                    _logger.LogAccess(ip, "ADMIN_LOGIN_SUCCESS", "", userAgent);
                    return true;
                }

                // Track failed attempts
                _loginAttempts.AddOrUpdate(ip, new List<DateTime> { now }, (key, list) =>
                {
                    list.RemoveAll(time => time < oneMinuteAgo);
                    list.Add(now);
                    return list;
                });

                var failedCount = _loginAttempts[ip].Count;

                // Brute force detection: 5 failed attempts per minute
                if (failedCount >= 5)
                {
                    BanIP(ip, TimeSpan.FromHours(1), "BRUTE_FORCE_DETECTED");
                    _logger.LogAccess(ip, "SECURITY_BRUTE_FORCE_DETECTED", $"Failed login attempts: {failedCount}", userAgent);

                    // Track for system-wide lockdown
                    _recentBreachAttempts.RemoveAll(time => time < fiveMinutesAgo);
                    _recentBreachAttempts.Add(now);

                    TriggerEmailAlert("Brute Force Attack Detected", $"IP {ip} attempted {failedCount} failed logins and has been banned.");

                    // System lockdown: 10 brute force attempts in 5 minutes
                    if (_recentBreachAttempts.Count >= 10)
                    {
                        _adminPanelLockdownUntil = now.AddMinutes(30);
                        _logger.LogAccess(ip, "SECURITY_ADMIN_LOCKDOWN", "Admin panel locked for 30 minutes due to multiple breach attempts", userAgent);

                        TriggerEmailAlert("CRITICAL: Admin Panel Locked",
                            $"Admin panel has been locked for 30 minutes due to {_recentBreachAttempts.Count} brute force attempts in 5 minutes. Latest attacker IP: {ip}");
                    }

                    return false;
                }

                _logger.LogAccess(ip, "ADMIN_LOGIN_FAILED", $"Attempt {failedCount}/5", userAgent);
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

            // Rate limit emails: max 1 every 30 minutes
            if (now.Subtract(_lastEmailSent).TotalMinutes >= 30)
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