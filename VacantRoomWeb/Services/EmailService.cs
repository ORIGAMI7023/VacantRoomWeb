// Services/EmailService.cs
using System.Text;
using System.Text.Json;

namespace VacantRoomWeb
{
    public interface IEmailService
    {
        Task<bool> SendSecurityAlertAsync(string alertType, string details, string ipAddress = null);
        Task<bool> SendSystemNotificationAsync(string subject, string message);
        Task<bool> SendCustomEmailAsync(string[] recipients, string subject, string body, bool isHtml = false);
        Task<bool> TestEmailServiceAsync();
        void SendSecurityAlert(string subject, string message); // 保持原有同步接口
    }

    public class EmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private readonly string _defaultRecipient;
        private DateTime _lastEmailSent = DateTime.MinValue;
        private readonly TimeSpan _emailCooldown = TimeSpan.FromMinutes(5); // 防止邮件轰炸

        public EmailService(HttpClient httpClient, IConfiguration configuration, ILogger<EmailService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _apiUrl = _configuration["Email:NotifyHubAPI:BaseUrl"] ?? "https://notify.origami7023.cn";
            _apiKey = _configuration["Email:NotifyHubAPI:ApiKey"] ?? "default-api-key-2024";
            _defaultRecipient = _configuration["Email:DefaultRecipient"] ?? "origami7023@gmail.com";

            // 配置 HttpClient
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<bool> SendSecurityAlertAsync(string alertType, string details, string ipAddress = null)
        {
            try
            {
                // 检查邮件发送频率限制
                if (DateTime.Now - _lastEmailSent < _emailCooldown)
                {
                    _logger.LogInformation("邮件发送被跳过：冷却时间内 ({Cooldown}分钟)", _emailCooldown.TotalMinutes);
                    return true; // 返回true避免影响主要逻辑
                }

                var subject = $"🚨 VacantRoomWeb 安全警报 - {alertType}";
                var body = GenerateSecurityAlertEmailBody(alertType, details, ipAddress);

                var success = await SendEmailAsync(new[] { _defaultRecipient }, subject, body, true, "SECURITY_ALERT");

                if (success)
                {
                    _lastEmailSent = DateTime.Now;
                    _logger.LogInformation("安全警报邮件发送成功：{AlertType}", alertType);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送安全警报邮件失败：{AlertType}", alertType);
                return false;
            }
        }

        public async Task<bool> SendSystemNotificationAsync(string subject, string message)
        {
            try
            {
                var fullSubject = $"📢 VacantRoomWeb 系统通知 - {subject}";
                var body = GenerateSystemNotificationEmailBody(subject, message);

                return await SendEmailAsync(new[] { _defaultRecipient }, fullSubject, body, true, "SYSTEM_NOTIFICATION");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送系统通知邮件失败：{Subject}", subject);
                return false;
            }
        }

        public async Task<bool> SendCustomEmailAsync(string[] recipients, string subject, string body, bool isHtml = false)
        {
            try
            {
                return await SendEmailAsync(recipients, subject, body, isHtml, "CUSTOM");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送自定义邮件失败");
                return false;
            }
        }

        public async Task<bool> TestEmailServiceAsync()
        {
            try
            {
                var subject = "VacantRoomWeb 邮件服务测试";
                var body = GenerateTestEmailBody();

                return await SendEmailAsync(new[] { _defaultRecipient }, subject, body, true, "TEST");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "邮件服务测试失败");
                return false;
            }
        }

        // 保持原有同步接口兼容性
        public void SendSecurityAlert(string subject, string message)
        {
            // 异步调用但不等待结果，避免阻塞
            Task.Run(async () =>
            {
                try
                {
                    await SendSecurityAlertAsync(subject, message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "同步安全警报发送失败");
                }
            });
        }

        private async Task<bool> SendEmailAsync(string[] recipients, string subject, string body, bool isHtml, string category)
        {
            try
            {
                var requestData = new
                {
                    to = recipients,
                    subject = subject,
                    body = body,
                    category = category,
                    isHtml = isHtml,
                    priority = category == "SECURITY_ALERT" ? 2 : 1 // 安全警报设为高优先级
                };

                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiUrl}/api/email/send", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("邮件发送成功：{Subject}", subject);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("邮件发送失败：{StatusCode} {Content}", response.StatusCode, errorContent);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "邮件API网络请求失败");
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "邮件发送超时");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "邮件发送异常");
                return false;
            }
        }

        private string GenerateSecurityAlertEmailBody(string alertType, string details, string ipAddress)
        {
            var serverTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var serverName = Environment.MachineName;

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ background-color: #dc3545; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; }}
        .alert-box {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 4px; margin: 20px 0; }}
        .details {{ background-color: #f8f9fa; padding: 15px; border-radius: 4px; font-family: monospace; }}
        .footer {{ background-color: #6c757d; color: white; padding: 15px; text-align: center; font-size: 12px; }}
        .priority {{ color: #dc3545; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🚨 安全警报通知</h1>
            <p class='priority'>优先级：高</p>
        </div>
        <div class='content'>
            <h2>警报类型：{alertType}</h2>
            
            <div class='alert-box'>
                <strong>⚠️ 检测到安全事件</strong><br>
                系统自动检测到可疑活动，请立即检查并采取必要措施。
            </div>

            <h3>事件详情：</h3>
            <div class='details'>
                {details}
            </div>

            {(string.IsNullOrEmpty(ipAddress) ? "" : $@"
            <h3>来源信息：</h3>
            <div class='details'>
                IP地址：{ipAddress}
            </div>")}

            <h3>系统信息：</h3>
            <div class='details'>
                服务器时间：{serverTime}<br>
                服务器名称：{serverName}<br>
                应用程序：VacantRoomWeb<br>
                环境：{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}
            </div>

            <h3>建议措施：</h3>
            <ul>
                <li>立即登录管理后台检查详细日志</li>
                <li>验证安全策略是否正常工作</li>
                <li>如有必要，手动封禁可疑IP地址</li>
                <li>监控后续活动</li>
            </ul>

            <p><strong>管理后台地址：</strong> <a href='https://admin.origami7023.cn/admin'>https://admin.origami7023.cn/admin</a></p>
        </div>
        <div class='footer'>
            此邮件由 VacantRoomWeb 安全监控系统自动发送<br>
            发送时间：{serverTime}
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateSystemNotificationEmailBody(string subject, string message)
        {
            var serverTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ background-color: #17a2b8; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; }}
        .message-box {{ background-color: #d1ecf1; border: 1px solid #bee5eb; padding: 15px; border-radius: 4px; margin: 20px 0; }}
        .footer {{ background-color: #6c757d; color: white; padding: 15px; text-align: center; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📢 系统通知</h1>
        </div>
        <div class='content'>
            <h2>{subject}</h2>
            
            <div class='message-box'>
                {message}
            </div>

            <p><strong>发送时间：</strong> {serverTime}</p>
            <p><strong>系统：</strong> VacantRoomWeb</p>
        </div>
        <div class='footer'>
            此邮件由 VacantRoomWeb 系统自动发送<br>
            发送时间：{serverTime}
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateTestEmailBody()
        {
            var serverTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; }}
        .success-box {{ background-color: #d4edda; border: 1px solid #c3e6cb; padding: 15px; border-radius: 4px; margin: 20px 0; }}
        .footer {{ background-color: #6c757d; color: white; padding: 15px; text-align: center; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✅ 邮件服务测试</h1>
        </div>
        <div class='content'>
            <div class='success-box'>
                <strong>邮件服务工作正常！</strong><br>
                如果您收到这封邮件，说明 VacantRoomWeb 的邮件通知系统已成功配置并正常工作。
            </div>

            <h3>测试信息：</h3>
            <ul>
                <li><strong>测试时间：</strong> {serverTime}</li>
                <li><strong>API地址：</strong> {_apiUrl}</li>
                <li><strong>收件人：</strong> {_defaultRecipient}</li>
                <li><strong>服务器：</strong> {Environment.MachineName}</li>
            </ul>

            <p>现在您可以确信安全警报和系统通知将正确发送。</p>
        </div>
        <div class='footer'>
            此邮件由 VacantRoomWeb 邮件服务测试功能发送<br>
            发送时间：{serverTime}
        </div>
    </div>
</body>
</html>";
        }
    }
}