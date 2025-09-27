// Services/ConfigurationService.cs
namespace VacantRoomWeb.Services
{
    public class AdminConfig
    {
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Salt { get; set; } = "";
        public string SecretKey { get; set; } = "";
    }

    public interface IConfigurationService
    {
        AdminConfig? GetAdminConfig();
        string GetEmailApiUrl();
        string GetEmailApiKey();
        string GetDefaultRecipient();
        int GetEmailCooldownMinutes();
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurationService> _logger;

        public ConfigurationService(IConfiguration configuration, ILogger<ConfigurationService> logger = null)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public AdminConfig? GetAdminConfig()
        {
            try
            {
                var config = new AdminConfig();

                // 优先从环境变量读取（生产环境）
                config.Username = _configuration["VACANTROOM_ADMIN_USERNAME"];
                config.PasswordHash = _configuration["VACANTROOM_ADMIN_PASSWORDHASH"];
                config.Salt = _configuration["VACANTROOM_ADMIN_SALT"];
                config.SecretKey = _configuration["VACANTROOM_ADMIN_SECRETKEY"];

                // 如果环境变量不存在，尝试从配置文件读取（开发环境）
                if (string.IsNullOrEmpty(config.Username))
                {
                    var adminSection = _configuration.GetSection("AdminConfig");
                    if (adminSection.Exists())
                    {
                        adminSection.Bind(config);
                    }
                    else
                    {
                        _logger?.LogWarning("Admin configuration not found in environment variables or appsettings");
                        return null;
                    }
                }

                // 验证配置完整性
                if (string.IsNullOrEmpty(config.Username) ||
                    string.IsNullOrEmpty(config.PasswordHash) ||
                    string.IsNullOrEmpty(config.Salt))
                {
                    _logger?.LogWarning("Admin configuration is incomplete");
                    return null;
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load admin configuration");
                return null;
            }
        }

        public string GetEmailApiUrl()
        {
            return _configuration["Email:NotifyHubAPI:BaseUrl"] ?? "";
        }

        public string GetEmailApiKey()
        {
            return _configuration["Email:NotifyHubAPI:ApiKey"] ?? "";
        }

        public string GetDefaultRecipient()
        {
            return _configuration["Email:DefaultRecipient"] ?? "";
        }

        public int GetEmailCooldownMinutes()
        {
            return _configuration.GetValue<int>("Email:CooldownMinutes", 5);
        }
    }
}