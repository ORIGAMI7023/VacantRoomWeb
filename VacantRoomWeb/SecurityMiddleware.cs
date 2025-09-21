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

            // 优化：只记录重要的访问，过滤掉静态资源和框架文件
            if (ShouldLogRequest(requestPath))
            {
                _logger.LogAccess(ip, "ACCESS", requestPath, userAgent);
            }

            await _next(context);
        }

        private bool ShouldLogRequest(string path)
        {
            // 不记录静态资源和框架文件
            var skipPatterns = new[]
            {
                "/_framework/",
                "/_blazor/",
                "/_content/",
                "/css/",
                "/js/",
                "/lib/",
                "/bootstrap/",
                "/favicon.ico",
                "/robots.txt",
                ".css",
                ".js",
                ".map",
                ".woff",
                ".woff2",
                ".ttf",
                ".eot",
                ".svg",
                ".png",
                ".jpg",
                ".jpeg",
                ".gif",
                ".ico"
            };

            foreach (var pattern in skipPatterns)
            {
                if (path.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // 只记录页面访问和重要的API调用
            return true;
        }
    }
}