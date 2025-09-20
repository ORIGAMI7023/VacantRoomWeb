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
                _logger.LogAccess(ip, "LOCALHOST_ADMIN_ACCESS", $"Host: {host}, Path: {path}");
                // Allow direct admin access on localhost
                await _next(context);
                return;
            }

            // Handle admin subdomain
            if (host.StartsWith("admin."))
            {
                _logger.LogAccess(ip, "ADMIN_SUBDOMAIN_ACCESS", $"Host: {host}, Path: {path}");

                // admin 根跳转到登录
                if (path == "/" || string.IsNullOrEmpty(path))
                {
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
                    _logger.LogAccess(ip, "ADMIN_PATH_REWRITE", $"Rewritten: {path} -> {newPath}");
                }
            }

            await _next(context);
        }
    }
}