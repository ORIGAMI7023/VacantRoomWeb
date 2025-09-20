using System.Security.Cryptography;
using System.Text;
using Microsoft.JSInterop;

namespace VacantRoomWeb
{
    public class AdminConfig
    {
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Salt { get; set; } = "";
    }

    public class AdminAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly SecurityService _securityService;
        private readonly EnhancedLoggingService _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminAuthService(
            IConfiguration configuration,
            SecurityService securityService,
            EnhancedLoggingService logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _securityService = securityService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public bool ValidateCredentials(string username, string password)
        {
            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";
            var userAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? "";

            var adminConfig = _configuration.GetSection("AdminConfig").Get<AdminConfig>();

            if (adminConfig == null || string.IsNullOrEmpty(adminConfig.Username))
            {
                _logger.LogAccess(ip, "ADMIN_CONFIG_ERROR", "Admin configuration not found", userAgent);
                return false;
            }

            bool isValid = adminConfig.Username == username &&
                          VerifyPassword(password, adminConfig.PasswordHash, adminConfig.Salt);

            // Always check with security service to track attempts
            _securityService.CheckLoginAttempt(ip, isValid, userAgent);

            return isValid;
        }

        public async Task SetAuthCookieAsync(string username, IJSRuntime jsRuntime)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            var cookieValue = CreateAuthToken(username);
            var expires = DateTime.UtcNow.AddDays(7);

            // 使用 JavaScript 设置 Cookie
            var secure = context.Request.IsHttps ? "secure; " : "";
            var cookieString = $"AdminAuth={cookieValue}; expires={expires:R}; path=/; {secure}samesite=strict";

            await jsRuntime.InvokeVoidAsync("eval", $"document.cookie = '{cookieString}'");

            var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";
            _logger.LogAccess(ip, "ADMIN_AUTH_COOKIE_SET", username);
        }

        public void SetAuthCookie(string username)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            try
            {
                var cookieValue = CreateAuthToken(username);
                var options = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.UtcNow.AddDays(7)
                };

                context.Response.Cookies.Append("AdminAuth", cookieValue, options);

                var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";
                _logger.LogAccess(ip, "ADMIN_AUTH_COOKIE_SET", username);
            }
            catch (InvalidOperationException)
            {
                // Response has already started, can't set cookie through HttpContext
                // This will be handled by the caller using JavaScript
                var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";
                _logger.LogAccess(ip, "ADMIN_AUTH_COOKIE_SET_FAILED", "Response already started");
                throw;
            }
        }

        public bool IsAuthenticated()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return false;

            var cookieValue = context.Request.Cookies["AdminAuth"];
            if (string.IsNullOrEmpty(cookieValue)) return false;

            return ValidateAuthToken(cookieValue);
        }

        public void SignOut()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            context.Response.Cookies.Delete("AdminAuth");

            var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";
            _logger.LogAccess(ip, "ADMIN_LOGOUT");
        }

        private string CreateAuthToken(string username)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var data = $"{username}:{timestamp}";
            var signature = ComputeHmac(data);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{data}:{signature}"));
        }

        private bool ValidateAuthToken(string token)
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = decoded.Split(':');

                if (parts.Length != 3) return false;

                var username = parts[0];
                var timestampStr = parts[1];
                var signature = parts[2];

                // Verify signature
                var data = $"{username}:{timestampStr}";
                var expectedSignature = ComputeHmac(data);
                if (signature != expectedSignature) return false;

                // Check expiration (7 days)
                if (long.TryParse(timestampStr, out var timestamp))
                {
                    var tokenTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                    if (DateTime.UtcNow.Subtract(tokenTime.DateTime).TotalDays > 7)
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private string ComputeHmac(string data)
        {
            var key = _configuration["AdminConfig:SecretKey"] ?? "DefaultSecretKey";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }

        private bool VerifyPassword(string password, string storedHash, string salt)
        {
            var hash = ComputePasswordHash(password, salt);
            return hash == storedHash;
        }

        public static string ComputePasswordHash(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var saltedPassword = salt + password;
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
            return Convert.ToBase64String(hash);
        }

        public static (string hash, string salt) CreatePasswordHash(string password)
        {
            var salt = GenerateSalt();
            var hash = ComputePasswordHash(password, salt);
            return (hash, salt);
        }

        private static string GenerateSalt()
        {
            var saltBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }

        public AdminConfig? GetAdminConfig()
        {
            return _configuration.GetSection("AdminConfig").Get<AdminConfig>();
        }

        public bool ValidatePassword(string password, string storedHash, string salt)
        {
            return VerifyPassword(password, storedHash, salt);
        }
    }
}