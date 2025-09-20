namespace VacantRoomWeb
{
    public class SecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly SecurityService _securityService;
        private readonly EnhancedLoggingService _logger;

        public SecurityMiddleware(RequestDelegate next, SecurityService securityService, EnhancedLoggingService logger)
        {
            _next = next;
            _securityService = securityService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";
            var userAgent = context.Request.Headers.UserAgent.ToString();
            var requestPath = context.Request.Path.ToString();

            // Check if IP is banned
            if (_securityService.IsIPBanned(ip))
            {
                _logger.LogAccess(ip, "ACCESS_DENIED_BANNED", requestPath, userAgent);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Access denied");
                return;
            }

            // Check admin panel lockdown
            if (requestPath.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) && _securityService.IsAdminPanelLocked())
            {
                _logger.LogAccess(ip, "ACCESS_DENIED_LOCKDOWN", requestPath, userAgent);
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("Admin panel temporarily unavailable");
                return;
            }

            // Rate limiting check
            if (!_securityService.CheckRateLimit(ip, userAgent))
            {
                context.Response.StatusCode = 429;
                await context.Response.WriteAsync("Too many requests");
                return;
            }

            // Log normal access
            _logger.LogAccess(ip, "ACCESS", requestPath, userAgent);

            await _next(context);
        }
    }
}