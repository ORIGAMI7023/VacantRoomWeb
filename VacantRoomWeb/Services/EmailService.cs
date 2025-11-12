// Services/EmailService.cs - 更新版本使用ConfigurationService
using System.Text;
using System.Text.Json;

namespace VacantRoomWeb.Services
{
    public class EmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationService _configService;  // 改用ConfigurationService
        private readonly ILogger<EmailService> _logger;
        private readonly Dictionary<string, DateTime> _lastSentTimes = new();
        private readonly object _cooldownLock = new();
        private readonly EnhancedLoggingService _fileLog;


        public EmailService(HttpClient httpClient, IConfigurationService configService, ILogger<EmailService> logger, EnhancedLoggingService fileLog)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;
            _fileLog = fileLog;

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }


        public async Task<bool> SendSecurityAlertAsync(string subject, string message, string ipAddress = null)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var formattedMessage = FormatSecurityAlert(subject, message, ipAddress, timestamp);

            return await SendEmailAsync(
                subject: $"[安全告警] {subject}",
                body: formattedMessage,
                category: "SECURITY",
                isHtml: true,
                priority: 1
            ).ConfigureAwait(false);
        }

        public async Task<bool> SendSystemNotificationAsync(string subject, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var formattedMessage = FormatSystemNotification(subject, message, timestamp);

            return await SendEmailAsync(
                subject: $"[系统通知] {subject}",
                body: formattedMessage,
                category: "NOTIFICATION",
                isHtml: true,
                priority: 2
            ).ConfigureAwait(false);
        }

        private string FormatSecurityAlert(string subject, string message, string ipAddress, string timestamp)
        {
            var serverName = Environment.MachineName;
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
        }}
        .header {{
            background: linear-gradient(135deg, #dc3545, #c82333);
            color: white;
            padding: 20px;
            border-radius: 8px 8px 0 0;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 24px;
        }}
        .content {{
            background: #fff;
            padding: 30px;
            border: 1px solid #dee2e6;
            border-top: none;
        }}
        .alert-box {{
            background: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 15px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .info-table {{
            width: 100%;
            margin: 20px 0;
            border-collapse: collapse;
        }}
        .info-table td {{
            padding: 10px;
            border-bottom: 1px solid #e9ecef;
        }}
        .info-table td:first-child {{
            font-weight: bold;
            width: 140px;
            color: #495057;
        }}
        .footer {{
            background: #f8f9fa;
            padding: 15px;
            text-align: center;
            color: #6c757d;
            font-size: 12px;
            border-radius: 0 0 8px 8px;
            border: 1px solid #dee2e6;
            border-top: none;
        }}
        .badge {{
            display: inline-block;
            padding: 4px 8px;
            background: #dc3545;
            color: white;
            border-radius: 4px;
            font-size: 12px;
            font-weight: bold;
        }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>🚨 安全告警</h1>
        <p style='margin: 5px 0 0 0; opacity: 0.9;'>{subject}</p>
    </div>
    
    <div class='content'>
        <div class='alert-box'>
            <strong>⚠️ 告警信息：</strong><br>
            {message}
        </div>
        
        <table class='info-table'>
            <tr>
                <td>🕐 发生时间</td>
                <td>{timestamp}</td>
            </tr>
            <tr>
                <td>🌐 来源IP</td>
                <td><code>{ipAddress ?? "未知"}</code></td>
            </tr>
            <tr>
                <td>🖥️ 服务器</td>
                <td>{serverName}</td>
            </tr>
            <tr>
                <td>📍 环境</td>
                <td><span class='badge'>{environment}</span></td>
            </tr>
            <tr>
                <td>🔗 系统</td>
                <td>VacantRoomWeb 教室查询系统</td>
            </tr>
        </table>
        
        <p style='margin-top: 20px; padding: 15px; background: #e7f3ff; border-left: 4px solid #007bff; border-radius: 4px;'>
            <strong>💡 提示：</strong> 请及时登录管理后台查看详细日志，必要时采取相应安全措施。
        </p>
    </div>
    
    <div class='footer'>
        <p style='margin: 0;'>此邮件由 VacantRoomWeb 安全监控系统自动发送</p>
        <p style='margin: 5px 0 0 0;'>服务器: {serverName} | 发送时间: {timestamp}</p>
    </div>
</body>
</html>";
        }

        private string FormatSystemNotification(string subject, string message, string timestamp)
        {
            var serverName = Environment.MachineName;
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var uptime = (DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).ToString(@"d\.hh\:mm\:ss");

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
        }}
        .header {{
            background: linear-gradient(135deg, #17a2b8, #138496);
            color: white;
            padding: 20px;
            border-radius: 8px 8px 0 0;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 24px;
        }}
        .content {{
            background: #fff;
            padding: 30px;
            border: 1px solid #dee2e6;
            border-top: none;
        }}
        .message-box {{
            background: #f8f9fa;
            padding: 20px;
            margin: 20px 0;
            border-radius: 4px;
            border-left: 4px solid #17a2b8;
        }}
        .info-table {{
            width: 100%;
            margin: 20px 0;
            border-collapse: collapse;
        }}
        .info-table td {{
            padding: 10px;
            border-bottom: 1px solid #e9ecef;
        }}
        .info-table td:first-child {{
            font-weight: bold;
            width: 140px;
            color: #495057;
        }}
        .footer {{
            background: #f8f9fa;
            padding: 15px;
            text-align: center;
            color: #6c757d;
            font-size: 12px;
            border-radius: 0 0 8px 8px;
            border: 1px solid #dee2e6;
            border-top: none;
        }}
        .badge {{
            display: inline-block;
            padding: 4px 8px;
            background: #17a2b8;
            color: white;
            border-radius: 4px;
            font-size: 12px;
            font-weight: bold;
        }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>📢 系统通知</h1>
        <p style='margin: 5px 0 0 0; opacity: 0.9;'>{subject}</p>
    </div>
    
    <div class='content'>
        <div class='message-box'>
            {message}
        </div>
        
        <table class='info-table'>
            <tr>
                <td>🕐 发送时间</td>
                <td>{timestamp}</td>
            </tr>
            <tr>
                <td>🖥️ 服务器</td>
                <td>{serverName}</td>
            </tr>
            <tr>
                <td>📍 环境</td>
                <td><span class='badge'>{environment}</span></td>
            </tr>
            <tr>
                <td>⏱️ 运行时长</td>
                <td>{uptime}</td>
            </tr>
            <tr>
                <td>🔗 系统</td>
                <td>VacantRoomWeb 教室查询系统</td>
            </tr>
        </table>
    </div>
    
    <div class='footer'>
        <p style='margin: 0;'>此邮件由 VacantRoomWeb 系统自动发送</p>
        <p style='margin: 5px 0 0 0;'>服务器: {serverName} | 发送时间: {timestamp}</p>
    </div>
</body>
</html>";
        }

        public async Task<bool> TestEmailServiceAsync()
        {
            try
            {
                string recipient = _configService.GetDefaultRecipient();
                return await SendEmailAsync(
                    subject: "VacantRoomWeb 基础连通性测试",
                    body: "这是基础连通性测试邮件",
                    category: "NOTIFICATION",
                    isHtml: false,
                    priority: 1,
                    to: string.IsNullOrWhiteSpace(recipient) ? null : new[] { recipient }
                ).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TestEmailServiceAsync failed");
                return false;
            }
        }


        public async Task<bool> SimpleTestEmailServiceAsync()
        {
            _logger.LogInformation("=== SimpleTestEmailServiceAsync called ===");
            try
            {
                string recipient = _configService.GetDefaultRecipient();
                return await SendEmailAsync(
                    subject: "VacantRoomWeb 调试测试",
                    body: "这是一封用于调试的测试邮件",
                    category: "NOTIFICATION",
                    isHtml: false,
                    priority: 1,
                    to: string.IsNullOrWhiteSpace(recipient) ? null : new[] { recipient }
                ).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SimpleTestEmailServiceAsync failed");
                return false;
            }
        }

        public void SendSecurityAlert(string subject, string message)
        {
            _logger.LogInformation("=== SendSecurityAlert (sync) called ===");
            _ = Task.Run(async () => await SendSecurityAlertAsync(subject, message));
        }

        public void SendSystemNotification(string subject, string message)
        {
            _logger.LogInformation("=== SendSystemNotification (sync) called ===");
            _ = Task.Run(async () => await SendSystemNotificationAsync(subject, message));
        }

        private async Task<bool> SendEmailAsync(
            string subject,
            string body,
            string category = "NOTIFICATION",
            bool isHtml = false,
            int priority = 1,
            IEnumerable<string> to = null,
            IEnumerable<string> cc = null,
            IEnumerable<string> bcc = null)
        {
            _logger.LogInformation("=== SendEmailAsync called: subject={Subject}, category={Category}, isHtml={IsHtml}, priority={Priority} ===",
                subject, category, isHtml, priority);

            try
            {
                string baseUrl = _configService.GetEmailApiUrl();
                string apiKey = _configService.GetEmailApiKey();
                string endpoint = _configService.GetEmailEndpointPath();
                string fallbackTo = _configService.GetDefaultRecipient();

                // 收件人兜底：如果未显式传入 to，则使用默认收件人
                var toList = (to != null && to.Any()) ? to.ToArray() : (string.IsNullOrWhiteSpace(fallbackTo) ? Array.Empty<string>() : new[] { fallbackTo });
                var ccList = (cc != null && cc.Any()) ? cc.ToArray() : null;
                var bccList = (bcc != null && bcc.Any()) ? bcc.ToArray() : null;

                string url = $"{baseUrl?.TrimEnd('/')}{endpoint}";

                _fileLog?.LogAccess("SERVER", "EMAIL_API_CONFIG",
                    $"BaseUrl={(string.IsNullOrWhiteSpace(baseUrl) ? "<EMPTY>" : baseUrl)}; Endpoint={endpoint}; " +
                    $"ApiKey={(string.IsNullOrEmpty(apiKey) ? "<EMPTY>" : $"SET({apiKey.Length})")}; " +
                    $"To=[{string.Join(",", toList)}]; Cc=[{(ccList == null ? "" : string.Join(",", ccList))}]; Bcc=[{(bccList == null ? "" : string.Join(",", bccList))}]",
                    "EmailService");

                if (string.IsNullOrWhiteSpace(baseUrl) || toList.Length == 0)
                {
                    _logger.LogError("Email configuration incomplete. BaseUrl='{BaseUrl}', To count={Count}", baseUrl, toList.Length);
                    _fileLog?.LogAccess("SERVER", "EMAIL_API_FAIL", "CONFIG_INCOMPLETE", "EmailService", endpoint);
                    return false;
                }

                // 按服务端规范构造 payload
                var payload = new
                {
                    to = toList,
                    cc = ccList,
                    bcc = bccList,
                    subject = subject,
                    body = body,
                    category = category,
                    isHtml = isHtml,
                    priority = priority
                };

                string json = System.Text.Json.JsonSerializer.Serialize(payload);
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrEmpty(apiKey))
                    request.Headers.Add("X-API-Key", apiKey);

                _logger.LogInformation("Sending POST {Url}", url);
                _fileLog?.LogAccess("SERVER", "EMAIL_API_REQUEST",
                    $"POST {url} -> subject='{(subject?.Length > 60 ? subject.Substring(0, 60) + "..." : subject)}' | category={category} | isHtml={isHtml} | priority={priority}",
                    "EmailService", endpoint);

                using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                string respBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Email API failed: {Status} {Reason}. Body: {Body}",
                        (int)response.StatusCode, response.ReasonPhrase, respBody);

                    _fileLog?.LogAccess("SERVER", "EMAIL_API_FAIL",
                        $"{(int)response.StatusCode}:{response.ReasonPhrase} | {(respBody?.Length > 300 ? respBody.Substring(0, 300) + "..." : respBody)}",
                        "EmailService", endpoint);
                    return false;
                }

                _logger.LogInformation("Email API success: {Status}", (int)response.StatusCode);
                _fileLog?.LogAccess("SERVER", "EMAIL_API_SUCCESS",
                    $"{(int)response.StatusCode} | {(respBody?.Length > 200 ? respBody.Substring(0, 200) + "..." : respBody)}",
                    "EmailService", endpoint);
                return true;
            }
            catch (TaskCanceledException tex)
            {
                _logger.LogError(tex, "Email API request timed out");
                _fileLog?.LogAccess("SERVER", "EMAIL_API_EXCEPTION", "TIMEOUT", "EmailService", "/api/email/send");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email API request threw exception");
                _fileLog?.LogAccess("SERVER", "EMAIL_API_EXCEPTION",
                    ex.Message?.Length > 300 ? ex.Message.Substring(0, 300) + "..." : ex.Message,
                    "EmailService", "/api/email/send");
                return false;
            }
        }



        // 放在 EmailService.cs 里（同类中）的小工具方法
        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        private bool CheckCooldown(string emailType)
        {
            lock (_cooldownLock)
            {
                var cooldownMinutes = _configService.GetEmailCooldownMinutes();
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
            return $@"<html><body><h2>{subject}</h2><p>{message}</p><p>IP: {ipAddress}</p><p>Time: {timestamp}</p></body></html>";
        }

        private string FormatSystemNotification(string subject, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return $@"<html><body><h2>{subject}</h2><p>{message}</p><p>Time: {timestamp}</p></body></html>";
        }

        private string FormatTestEmail()
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return $@"<html><body><h2>Email Service Test</h2><p>VacantRoomWeb email service is working correctly.</p><p>Test time: {timestamp}</p></body></html>";
        }
    }
}