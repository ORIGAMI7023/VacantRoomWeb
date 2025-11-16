// Services/EmailSettingsService.cs
using System.Text.Json;
using VacantRoomWeb.Models;

namespace VacantRoomWeb.Services
{
    public class EmailSettingsService
    {
        private readonly string _settingsFilePath;
        private readonly ILogger<EmailSettingsService> _logger;
        private EmailNotificationSettings _settings;
        private readonly object _lock = new();

        public EmailSettingsService(ILogger<EmailSettingsService> logger)
        {
            _logger = logger;
            _settingsFilePath = Path.Combine("Data", "email-settings.json");

            // 确保Data目录存在
            var dataDir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dataDir) && !Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            // 加载设置
            _settings = LoadSettings();
        }

        private EmailNotificationSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<EmailNotificationSettings>(json);
                    if (settings != null)
                    {
                        _logger.LogInformation("Email settings loaded from {Path}", _settingsFilePath);
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load email settings from {Path}", _settingsFilePath);
            }

            // 返回默认设置（全部启用）
            _logger.LogInformation("Using default email settings (all alerts enabled)");
            return new EmailNotificationSettings
            {
                EnableDDoSAlerts = true,
                EnableBruteForceAlerts = true,
                EnableSystemLockdownAlerts = true,
                EnableIPBanAlerts = true
            };
        }

        public EmailNotificationSettings GetSettings()
        {
            lock (_lock)
            {
                return new EmailNotificationSettings
                {
                    EnableDDoSAlerts = _settings.EnableDDoSAlerts,
                    EnableBruteForceAlerts = _settings.EnableBruteForceAlerts,
                    EnableSystemLockdownAlerts = _settings.EnableSystemLockdownAlerts,
                    EnableIPBanAlerts = _settings.EnableIPBanAlerts
                };
            }
        }

        public bool SaveSettings(EmailNotificationSettings settings)
        {
            lock (_lock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    File.WriteAllText(_settingsFilePath, json);

                    // 更新内存中的设置
                    _settings = new EmailNotificationSettings
                    {
                        EnableDDoSAlerts = settings.EnableDDoSAlerts,
                        EnableBruteForceAlerts = settings.EnableBruteForceAlerts,
                        EnableSystemLockdownAlerts = settings.EnableSystemLockdownAlerts,
                        EnableIPBanAlerts = settings.EnableIPBanAlerts
                    };

                    _logger.LogInformation("Email settings saved to {Path}", _settingsFilePath);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save email settings to {Path}", _settingsFilePath);
                    return false;
                }
            }
        }

        // 检查特定类型的警报是否启用
        public bool IsDDoSAlertEnabled() => GetSettings().EnableDDoSAlerts;
        public bool IsBruteForceAlertEnabled() => GetSettings().EnableBruteForceAlerts;
        public bool IsSystemLockdownAlertEnabled() => GetSettings().EnableSystemLockdownAlerts;
        public bool IsIPBanAlertEnabled() => GetSettings().EnableIPBanAlerts;
    }
}
