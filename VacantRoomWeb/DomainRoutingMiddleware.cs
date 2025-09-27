namespace VacantRoomWeb
{
    public class DomainRoutingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly EnhancedLoggingService _logger;

        public DomainRoutingMiddleware(RequestDelegate next, EnhancedLoggingService logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var host = context.Request.Host.Host.ToLowerInvariant();
            var path = context.Request.Path.ToString();
            var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";

            // Allow localhost admin access in development
            if (host.Contains("localhost") && path.StartsWith("/admin"))
            {
                // 只记录关键的本地管理访问，避免重复
                if (IsKeyAdminPath(path))
                {
                    _logger.LogAccess(ip, "LOCALHOST_ADMIN_ACCESS", $"Host: {host}, Path: {path}");
                }
                await _next(context);
                return;
            }

            // Remove the admin subdomain handling since we're switching to main domain
            // The admin routes will now be handled directly on the main domain

            // Log access for key admin paths on main domain
            if (path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) && IsKeyAdminPath(path))
            {
                _logger.LogAccess(ip, "ADMIN_ACCESS", $"Host: {host}, Path: {path}");
            }

            await _next(context);
        }

        private bool IsKeyAdminPath(string path)
        {
            // 只记录真正关键的管理路径
            var keyPaths = new[]
            {
                "/admin",
                "/admin/",
                "/admin/login",
                "/admin/dashboard",
                "/admin/email-test"
            };

            return keyPaths.Any(keyPath =>
                path.Equals(keyPath, StringComparison.OrdinalIgnoreCase));
        }
    }
}