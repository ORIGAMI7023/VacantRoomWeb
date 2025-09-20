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

                // Redirect root admin domain to login
                if (path == "/" || string.IsNullOrEmpty(path))
                {
                    context.Response.Redirect("/admin/login");
                    return;
                }

                // Ensure admin paths are properly routed
                if (!path.StartsWith("/admin"))
                {
                    var newPath = "/admin" + path;
                    context.Request.Path = newPath;
                    _logger.LogAccess(ip, "ADMIN_PATH_REWRITE", $"Rewritten: {path} -> {newPath}");
                }
            }
            else
            {
                // Block access to admin paths from main domain (except localhost)
                if (path.StartsWith("/admin") && !host.Contains("localhost"))
                {
                    _logger.LogAccess(ip, "ADMIN_ACCESS_BLOCKED", $"Admin access attempt from main domain: {host}");
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("Not Found");
                    return;
                }
            }

            await _next(context);
        }
    }
}