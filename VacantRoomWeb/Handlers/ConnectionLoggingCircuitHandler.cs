using Microsoft.AspNetCore.Components.Server.Circuits;
using VacantRoomWeb.Services;

namespace VacantRoomWeb.Handlers
{
    public class ConnectionLoggingCircuitHandler : CircuitHandler
    {
        private readonly ConnectionCounterService _counter;
        private readonly ClientConnectionTracker _ipTracker;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly EnhancedLoggingService _logger;

        // 用于跟踪每个IP的连接情况，避免重复记录
        private static readonly Dictionary<string, DateTime> _lastLoggedConnection = new();
        private static readonly object _logLock = new();

        public ConnectionLoggingCircuitHandler(
            ConnectionCounterService counter,
            ClientConnectionTracker ipTracker,
            IHttpContextAccessor httpContextAccessor,
            EnhancedLoggingService logger)
        {
            _counter = counter;
            _ipTracker = ipTracker;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";
            var userAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? "";

            _ipTracker.SetClientIp(circuit.Id, ip);

            var count = _counter.Increment();

            // 智能记录：同一IP在30秒内的多次连接只记录一次
            lock (_logLock)
            {
                var now = DateTime.Now;
                var shouldLog = false;

                if (!_lastLoggedConnection.ContainsKey(ip))
                {
                    shouldLog = true;
                }
                else if (now.Subtract(_lastLoggedConnection[ip]).TotalSeconds > 30)
                {
                    shouldLog = true;
                }

                if (shouldLog)
                {
                    _lastLoggedConnection[ip] = now;
                    _logger.LogAccess(ip, "BLAZOR_CONNECTION_UP", $"Active connections: {count}", TruncateUserAgent(userAgent));

                    // 清理旧的记录（避免内存泄漏）
                    CleanupOldConnections();
                }
            }

            return Task.CompletedTask;
        }

        public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            var ip = _ipTracker.GetClientIp(circuit.Id);

            if (ip != null)
            {
                _ipTracker.Remove(circuit.Id);
                var count = _counter.Decrement();

                // 连接断开时也采用相同的策略，避免重复记录
                lock (_logLock)
                {
                    var now = DateTime.Now;
                    var shouldLog = false;

                    if (_lastLoggedConnection.ContainsKey(ip))
                    {
                        if (now.Subtract(_lastLoggedConnection[ip]).TotalSeconds > 10) // 断开连接的间隔更短
                        {
                            shouldLog = true;
                            _lastLoggedConnection[ip] = now;
                        }
                    }
                    else
                    {
                        shouldLog = true;
                        _lastLoggedConnection[ip] = now;
                    }

                    if (shouldLog)
                    {
                        _logger.LogAccess(ip, "BLAZOR_CONNECTION_DOWN", $"Active connections: {count}");
                    }
                }
            }

            return Task.CompletedTask;
        }

        private void CleanupOldConnections()
        {
            var cutoff = DateTime.Now.AddMinutes(-5);
            var keysToRemove = _lastLoggedConnection
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _lastLoggedConnection.Remove(key);
            }
        }

        private string TruncateUserAgent(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "";
            return userAgent.Length > 50 ? userAgent.Substring(0, 50) + "..." : userAgent;
        }
    }
}