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

            // Handle admin subdomain
            if (host.StartsWith("admin."))
            {
                // admin 根跳转到登录
                if (path == "/" || string.IsNullOrEmpty(path))
                {
                    _logger.LogAccess(ip, "ADMIN_SUBDOMAIN_REDIRECT", $"Host: {host} -> /admin/login");
                    context.Response.Redirect("/admin/login");
                    return;
                }

                // 不要重写这些系统路径
                bool isSystemPath =
                    path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase);

                // 仅当既不是 /admin 开头、也不是系统路径时，才加前缀
                if (!path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) && !isSystemPath)
                {
                    var newPath = "/admin" + path;
                    context.Request.Path = newPath;

                    // 只记录重要的路径重写
                    if (IsKeyAdminPath(newPath))
                    {
                        _logger.LogAccess(ip, "ADMIN_PATH_REWRITE", $"{path} -> {newPath}");
                    }
                }
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
                "/admin/dashboard"
            };

            return keyPaths.Any(keyPath =>
                path.Equals(keyPath, StringComparison.OrdinalIgnoreCase));
        }
    }
}