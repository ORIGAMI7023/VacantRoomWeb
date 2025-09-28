// Services/ConfigurationService.cs - 完整修复版本
namespace VacantRoomWeb.Services
{
    // AdminConfig 类定义
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
        Dictionary<string, string> GetEmailConfigDebugInfo();
        string GetEmailEndpointPath();
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

        public string GetEmailApiUrl()
        {
            var envValue = _configuration["VACANTROOM_EMAIL_BASEURL"];
            var configValue = _configuration["Email:NotifyHubAPI:BaseUrl"];

            _logger?.LogInformation("Email API URL - Env: {EnvValue}, Config: {ConfigValue}",
                envValue ?? "NULL", configValue ?? "NULL");

            return envValue ?? configValue ?? "";
        }

        public string GetEmailEndpointPath()
        {
            string envValue = _configuration["VACANTROOM_EMAIL_ENDPOINT"];
            string configValue = _configuration["Email:NotifyHubAPI:EndpointPath"];

            // 默认改为 /api/email/send
            string final = (envValue ?? configValue ?? "/api/email/send").Trim();
            if (!final.StartsWith("/")) final = "/" + final;

            _logger?.LogInformation("Email EndpointPath - Env:{Env} Config:{Cfg} Final:{Final}",
                envValue ?? "NULL", configValue ?? "NULL", final);

            return final;
        }

        public string GetEmailApiKey()
        {
            var envValue = _configuration["VACANTROOM_EMAIL_APIKEY"];
            var configValue = _configuration["Email:NotifyHubAPI:ApiKey"];

            _logger?.LogInformation("Email API Key - Env: {EnvExists}, Config: {ConfigExists}",
                !string.IsNullOrEmpty(envValue) ? "SET" : "NULL",
                !string.IsNullOrEmpty(configValue) ? "SET" : "NULL");

            return envValue ?? configValue ?? "";
        }

        public string GetDefaultRecipient()
        {
            var envValue = _configuration["VACANTROOM_EMAIL_RECIPIENT"];
            var configValue = _configuration["Email:DefaultRecipient"];

            _logger?.LogInformation("Email Recipient - Env: {EnvValue}, Config: {ConfigValue}",
                envValue ?? "NULL", configValue ?? "NULL");

            return envValue ?? configValue ?? "";
        }


        public int GetEmailCooldownMinutes()
        {
            return _configuration.GetValue<int>("Email:CooldownMinutes", 5);
        }

        // 获取完整的调试信息
        public Dictionary<string, string> GetEmailConfigDebugInfo()
        {
            var debug = new Dictionary<string, string>();

            // 环境变量检查
            debug["Env_BaseUrl"] = _configuration["VACANTROOM_EMAIL_BASEURL"] ?? "NULL";
            debug["Env_ApiKey"] = string.IsNullOrEmpty(_configuration["VACANTROOM_EMAIL_APIKEY"]) ? "NULL" : "SET";
            debug["Env_Recipient"] = _configuration["VACANTROOM_EMAIL_RECIPIENT"] ?? "NULL";

            // 配置文件检查
            debug["Config_BaseUrl"] = _configuration["Email:NotifyHubAPI:BaseUrl"] ?? "NULL";
            debug["Config_ApiKey"] = string.IsNullOrEmpty(_configuration["Email:NotifyHubAPI:ApiKey"]) ? "NULL" : "SET";
            debug["Config_Recipient"] = _configuration["Email:DefaultRecipient"] ?? "NULL";

            // 最终值
            debug["Final_BaseUrl"] = GetEmailApiUrl();
            debug["Final_ApiKey"] = string.IsNullOrEmpty(GetEmailApiKey()) ? "NULL" : "SET";
            debug["Final_Recipient"] = GetDefaultRecipient();

            // 环境信息
            debug["Environment"] = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "NULL";
            debug["MachineName"] = Environment.MachineName;

            return debug;
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

    }
}