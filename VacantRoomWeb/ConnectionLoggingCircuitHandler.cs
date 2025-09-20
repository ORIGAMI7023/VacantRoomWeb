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
            _logger.LogAccess(ip, "BLAZOR_CONNECTION_UP", $"Active connections: {count}", userAgent);

            return Task.CompletedTask;
        }

        public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            var ip = _ipTracker.GetClientIp(circuit.Id);

            if (ip != null)
            {
                _ipTracker.Remove(circuit.Id);
                var count = _counter.Decrement();
                _logger.LogAccess(ip, "BLAZOR_CONNECTION_DOWN", $"Active connections: {count}");
            }
            else
            {
                _logger.LogAccess("Unknown", "BLAZOR_CONNECTION_DOWN", "IP not tracked");
            }

            return Task.CompletedTask;
        }
    }
}