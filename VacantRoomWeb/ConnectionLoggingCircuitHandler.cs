using Microsoft.AspNetCore.Components.Server.Circuits;

namespace VacantRoomWeb
{
    public class ConnectionLoggingCircuitHandler : CircuitHandler
    {
        private readonly ConnectionCounterService _counter;
        private readonly ClientConnectionTracker _ipTracker;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly EnhancedLoggingService _logger;

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

            // 简化日志：只记录新的用户连接，不记录每个 SignalR 连接
            if (count % 10 == 1 || count <= 5) // 仅在连接数变化较大时记录
            {
                _logger.LogAccess(ip, "BLAZOR_CONNECTION_UP", $"Active connections: {count}", TruncateUserAgent(userAgent));
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

                // 简化日志：只记录重要的连接断开
                if (count % 10 == 0 || count <= 5)
                {
                    _logger.LogAccess(ip, "BLAZOR_CONNECTION_DOWN", $"Active connections: {count}");
                }
            }

            return Task.CompletedTask;
        }

        private string TruncateUserAgent(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "";
            return userAgent.Length > 50 ? userAgent.Substring(0, 50) + "..." : userAgent;
        }
    }
}