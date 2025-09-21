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
                // 只记录重要的本地管理访问，不记录静态资源
                if (IsImportantAdminPath(path))
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
                    _logger.LogAccess(ip, "ADMIN_SUBDOMAIN_REDIRECT", $"Host: {host}, Redirecting to /admin/login");
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

                    // 只记录路径重写，不记录普通访问
                    if (IsImportantAdminPath(newPath))
                    {
                        _logger.LogAccess(ip, "ADMIN_PATH_REWRITE", $"Rewritten: {path} -> {newPath}");
                    }
                }
                else if (IsImportantAdminPath(path))
                {
                    // 记录重要的admin子域访问
                    _logger.LogAccess(ip, "ADMIN_SUBDOMAIN_ACCESS", $"Host: {host}, Path: {path}");
                }
            }

            await _next(context);
        }

        private bool IsImportantAdminPath(string path)
        {
            // 只记录重要的管理路径，排除静态资源
            var importantPaths = new[]
            {
                "/admin/login",
                "/admin/dashboard",
                "/admin",
                "/admin/"
            };

            // 精确匹配重要路径
            foreach (var importantPath in importantPaths)
            {
                if (path.Equals(importantPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 如果是 /admin 开头且不是静态资源，也记录
            if (path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
            {
                var staticExtensions = new[] { ".css", ".js", ".map", ".woff", ".woff2", ".ttf", ".eot", ".svg", ".png", ".jpg", ".jpeg", ".gif", ".ico" };
                return !staticExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }
    }
}