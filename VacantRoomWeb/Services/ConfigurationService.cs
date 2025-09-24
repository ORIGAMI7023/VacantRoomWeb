// Services/ConfigurationService.cs
namespace VacantRoomWeb.Services
{
    public interface IConfigurationService
    {
        AdminConfig GetAdminConfig();
        EmailConfig GetEmailConfig();
        SecurityConfig GetSecurityConfig();
        LoggingConfig GetLoggingConfig();
        SystemConfig GetSystemConfig();
        string GetConnectionString(string name);
        T GetSection<T>(string sectionName) where T : new();
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurationService> _logger;

        public ConfigurationService(IConfiguration configuration, ILogger<ConfigurationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public AdminConfig GetAdminConfig()
        {
            try
            {
                // 优先从环境变量读取（web.config设置的）
                var config = new AdminConfig
                {
                    Username = GetConfigValue("AdminConfig:Username", "AdminConfig__Username"),
                    PasswordHash = GetConfigValue("AdminConfig:PasswordHash", "AdminConfig__PasswordHash"),
                    Salt = GetConfigValue("AdminConfig:Salt", "AdminConfig__Salt"),
                    SecretKey = GetConfigValue("AdminConfig:SecretKey", "AdminConfig__SecretKey")
                };

                if (string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.PasswordHash))
                {
                    _logger.LogWarning("管理员配置不完整，请检查web.config设置");
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取管理员配置失败");
                return new AdminConfig();
            }
        }

        public EmailConfig GetEmailConfig()
        {
            try
            {
                var config = new EmailConfig
                {
                    NotifyHubAPI = new NotifyHubAPIConfig
                    {
                        BaseUrl = GetConfigValue("Email:NotifyHubAPI:BaseUrl", "Email__NotifyHubAPI__BaseUrl") ?? "https://notify.origami7023.cn",
                        ApiKey = GetConfigValue("Email:NotifyHubAPI:ApiKey", "Email__NotifyHubAPI__ApiKey") ?? "default-api-key-2024"
                    },
                    DefaultRecipient = GetConfigValue("Email:DefaultRecipient", "Email__DefaultRecipient") ?? "origami7023@gmail.com",
                    CooldownMinutes = _configuration.GetValue<int>("Email:CooldownMinutes", 5),
                    MaxRetries = _configuration.GetValue<int>("Email:MaxRetries", 3),
                    TimeoutSeconds = _configuration.GetValue<int>("Email:TimeoutSeconds", 30)
                };

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取邮件配置失败");
                return new EmailConfig();
            }
        }

        public SecurityConfig GetSecurityConfig()
        {
            return new SecurityConfig
            {
                DDoSThreshold = _configuration.GetValue<int>("Security:DDoSThreshold", 120),
                LoginFailureThreshold = _configuration.GetValue<int>("Security:LoginFailureThreshold", 8),
                LoginFailureWindow = _configuration.GetValue<int>("Security:LoginFailureWindow", 5),
                BanDuration = _configuration.GetValue<int>("Security:BanDuration", 30),
                LockdownDuration = _configuration.GetValue<int>("Security:LockdownDuration", 15)
            };
        }

        public LoggingConfig GetLoggingConfig()
        {
            return new LoggingConfig
            {
                MaxRecentLogs = _configuration.GetValue<int>("Logging:MaxRecentLogs", 500),
                LogRetentionDays = _configuration.GetValue<int>("Logging:LogRetentionDays", 30),
                LogDirectory = _configuration.GetValue<string>("Logging:LogDirectory", "Logs")
            };
        }

        public SystemConfig GetSystemConfig()
        {
            return new SystemConfig
            {
                RefreshIntervalSeconds = _configuration.GetValue<int>("System:RefreshIntervalSeconds", 30),
                MaxConnectionsToTrack = _configuration.GetValue<int>("System:MaxConnectionsToTrack", 1000)
            };
        }

        public string GetConnectionString(string name)
        {
            return _configuration.GetConnectionString(name);
        }

        public T GetSection<T>(string sectionName) where T : new()
        {
            var section = _configuration.GetSection(sectionName);
            var config = new T();
            section.Bind(config);
            return config;
        }

        // 私有方法：优先从环境变量读取，回退到appsettings
        private string GetConfigValue(string appSettingsKey, string environmentKey)
        {
            // 优先从环境变量读取（web.config设置的）
            var envValue = Environment.GetEnvironmentVariable(environmentKey);
            if (!string.IsNullOrEmpty(envValue))
            {
                _logger.LogDebug("使用环境变量配置: {Key}", environmentKey);
                return envValue;
            }

            // 回退到appsettings.json
            var appValue = _configuration[appSettingsKey];
            if (!string.IsNullOrEmpty(appValue))
            {
                _logger.LogDebug("使用appsettings配置: {Key}", appSettingsKey);
                return appValue;
            }

            _logger.LogWarning("配置项未找到: {AppSettingsKey} / {EnvironmentKey}", appSettingsKey, environmentKey);
            return null;
        }
    }

    // 配置模型类
    public class AdminConfig
    {
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Salt { get; set; } = "";
        public string SecretKey { get; set; } = "";
    }

    public class EmailConfig
    {
        public NotifyHubAPIConfig NotifyHubAPI { get; set; } = new();
        public string DefaultRecipient { get; set; } = "";
        public int CooldownMinutes { get; set; } = 5;
        public int MaxRetries { get; set; } = 3;
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class NotifyHubAPIConfig
    {
        public string BaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
    }

    public class SecurityConfig
    {
        public int DDoSThreshold { get; set; } = 120;
        public int LoginFailureThreshold { get; set; } = 8;
        public int LoginFailureWindow { get; set; } = 5;
        public int BanDuration { get; set; } = 30;
        public int LockdownDuration { get; set; } = 15;
    }

    public class LoggingConfig
    {
        public int MaxRecentLogs { get; set; } = 500;
        public int LogRetentionDays { get; set; } = 30;
        public string LogDirectory { get; set; } = "Logs";
    }

    public class SystemConfig
    {
        public int RefreshIntervalSeconds { get; set; } = 30;
        public int MaxConnectionsToTrack { get; set; } = 1000;
    }
}