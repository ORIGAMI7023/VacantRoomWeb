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
                _configuration.GetSection("AdminConfig").Bind(config);

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