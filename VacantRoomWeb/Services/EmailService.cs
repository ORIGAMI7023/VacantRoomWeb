// Services/EmailService.cs
using System.Text;
using System.Text.Json;

namespace VacantRoomWeb.Services
{
    public class EmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly Dictionary<string, DateTime> _lastSentTimes = new();
        private readonly object _cooldownLock = new();

        public EmailService(HttpClient httpClient, IConfiguration configuration, ILogger<EmailService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            var timeoutSeconds = _configuration.GetValue<int>("Email:TimeoutSeconds", 30);
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }

        public async Task<bool> SendSecurityAlertAsync(string subject, string message, string ipAddress = null)
        {
            if (!CheckCooldown("security_alert"))
            {
                _logger.LogWarning("Security alert email skipped due to cooldown");
                return false;
            }

            var emailContent = FormatSecurityAlert(subject, message, ipAddress);
            return await SendEmailAsync(subject, emailContent);
        }

        public async Task<bool> SendSystemNotificationAsync(string subject, string message)
        {
            if (!CheckCooldown("system_notification"))
            {
                _logger.LogWarning("System notification email skipped due to cooldown");
                return false;
            }

            var emailContent = FormatSystemNotification(subject, message);
            return await SendEmailAsync(subject, emailContent);
        }

        public async Task<bool> TestEmailServiceAsync()
        {
            var subject = "VacantRoomWeb 邮件服务测试";
            var message = FormatTestEmail();
            return await SendEmailAsync(subject, message);
        }

        public void SendSecurityAlert(string subject, string message)
        {
            _ = Task.Run(async () => await SendSecurityAlertAsync(subject, message));
        }

        public void SendSystemNotification(string subject, string message)
        {
            _ = Task.Run(async () => await SendSystemNotificationAsync(subject, message));
        }

        private async Task<bool> SendEmailAsync(string subject, string content)
        {
            try
            {
                var apiUrl = _configuration["Email:NotifyHubAPI:BaseUrl"];
                var apiKey = _configuration["Email:NotifyHubAPI:ApiKey"];
                var recipient = _configuration["Email:DefaultRecipient"];

                if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(recipient))
                {
                    _logger.LogError("Email configuration is incomplete");
                    return false;
                }

                var payload = new
                {
                    to = recipient,
                    subject = subject,
                    content = content,
                    contentType = "html"
                };

                var jsonContent = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                if (!string.IsNullOrEmpty(apiKey))
                {
                    httpContent.Headers.Add("X-API-Key", apiKey);
                }

                var maxRetries = _configuration.GetValue<int>("Email:MaxRetries", 3);

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        var response = await _httpClient.PostAsync($"{apiUrl}/api/send", httpContent);

                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Email sent successfully: {Subject}", subject);
                            return true;
                        }

                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning("Email send failed (attempt {Attempt}/{MaxRetries}): {StatusCode} - {Error}",
                            attempt, maxRetries, response.StatusCode, errorContent);

                        if (attempt < maxRetries)
                        {
                            await Task.Delay(1000 * attempt); // Exponential backoff
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning("HTTP request failed (attempt {Attempt}/{MaxRetries}): {Error}",
                            attempt, maxRetries, ex.Message);

                        if (attempt < maxRetries)
                        {
                            await Task.Delay(1000 * attempt);
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email: {Subject}", subject);
                return false;
            }
        }

        private bool CheckCooldown(string emailType)
        {
            lock (_cooldownLock)
            {
                var cooldownMinutes = _configuration.GetValue<int>("Email:CooldownMinutes", 5);
                var now = DateTime.Now;

                if (_lastSentTimes.TryGetValue(emailType, out var lastSent))
                {
                    if (now.Subtract(lastSent).TotalMinutes < cooldownMinutes)
                    {
                        return false;
                    }
                }

                _lastSentTimes[emailType] = now;
                return true;
            }
        }

        private string FormatSecurityAlert(string subject, string message, string ipAddress)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>安全警报</title>
</head>
<body style='font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;'>
    <div style='max-width: 600px; margin: 0 auto; background-color: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
        <div style='background-color: #dc3545; color: white; padding: 20px; text-align: center;'>
            <h1 style='margin: 0; font-size: 24px;'>🚨 安全警报</h1>
        </div>
        <div style='padding: 30px;'>
            <h2 style='color: #dc3545; margin-top: 0;'>{subject}</h2>
            <div style='background-color: #f8f9fa; padding: 15px; border-left: 4px solid #dc3545; margin: 20px 0;'>
                <p style='margin: 0; line-height: 1.6;'>{message}</p>
            </div>
            {(string.IsNullOrEmpty(ipAddress) ? "" : $@"
            <div style='margin: 20px 0;'>
                <strong>IP地址：</strong> <code style='background-color: #f8f9fa; padding: 2px 6px; border-radius: 3px;'>{ipAddress}</code>
            </div>")}
            <div style='margin: 20px 0; font-size: 14px; color: #6c757d;'>
                <strong>时间：</strong> {timestamp}<br>
                <strong>系统：</strong> VacantRoomWeb 教室查询系统<br>
                <strong>服务器：</strong> {Environment.MachineName}
            </div>
        </div>
        <div style='background-color: #f8f9fa; padding: 15px; text-align: center; font-size: 12px; color: #6c757d;'>
            此邮件由 VacantRoomWeb 安全监控系统自动发送<br>
            请勿回复此邮件
        </div>
    </div>
</body>
</html>";
        }

        private string FormatSystemNotification(string subject, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>系统通知</title>
</head>
<body style='font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;'>
    <div style='max-width: 600px; margin: 0 auto; background-color: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
        <div style='background-color: #17a2b8; color: white; padding: 20px; text-align: center;'>
            <h1 style='margin: 0; font-size: 24px;'>📢 系统通知</h1>
        </div>
        <div style='padding: 30px;'>
            <h2 style='color: #17a2b8; margin-top: 0;'>{subject}</h2>
            <div style='background-color: #f8f9fa; padding: 15px; border-left: 4px solid #17a2b8; margin: 20px 0;'>
                <p style='margin: 0; line-height: 1.6;'>{message}</p>
            </div>
            <div style='margin: 20px 0; font-size: 14px; color: #6c757d;'>
                <strong>时间：</strong> {timestamp}<br>
                <strong>系统：</strong> VacantRoomWeb 教室查询系统<br>
                <strong>服务器：</strong> {Environment.MachineName}
            </div>
        </div>
        <div style='background-color: #f8f9fa; padding: 15px; text-align: center; font-size: 12px; color: #6c757d;'>
            此邮件由 VacantRoomWeb 系统自动发送<br>
            请勿回复此邮件
        </div>
    </div>
</body>
</html>";
        }

        private string FormatTestEmail()
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>邮件服务测试</title>
</head>
<body style='font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5;'>
    <div style='max-width: 600px; margin: 0 auto; background-color: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
        <div style='background-color: #28a745; color: white; padding: 20px; text-align: center;'>
            <h1 style='margin: 0; font-size: 24px;'>✅ 邮件服务测试</h1>
        </div>
        <div style='padding: 30px;'>
            <h2 style='color: #28a745; margin-top: 0;'>测试成功</h2>
            <div style='background-color: #f8f9fa; padding: 15px; border-left: 4px solid #28a745; margin: 20px 0;'>
                <p style='margin: 0; line-height: 1.6;'>VacantRoomWeb 邮件通知服务运行正常。</p>
                <p style='margin: 10px 0 0 0; line-height: 1.6;'>如果您收到了这封邮件，说明邮件发送功能已正确配置并可以正常工作。</p>
            </div>
            <div style='margin: 20px 0; font-size: 14px; color: #6c757d;'>
                <strong>测试时间：</strong> {timestamp}<br>
                <strong>系统版本：</strong> VacantRoomWeb v1.0.0<br>
                <strong>服务器：</strong> {Environment.MachineName}<br>
                <strong>运行环境：</strong> .NET 8 / Blazor Server
            </div>
        </div>
        <div style='background-color: #f8f9fa; padding: 15px; text-align: center; font-size: 12px; color: #6c757d;'>
            此邮件由 VacantRoomWeb 邮件服务测试功能发送<br>
            请勿回复此邮件
        </div>
    </div>
</body>
</html>";
        }
    }
}