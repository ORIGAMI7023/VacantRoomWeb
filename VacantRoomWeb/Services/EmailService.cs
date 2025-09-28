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
            return await SendEmailAsync($"[安全告警] {subject}", message).ConfigureAwait(false);
        }

        public async Task<bool> SendSystemNotificationAsync(string subject, string message)
        {
            return await SendEmailAsync($"[系统通知] {subject}", message).ConfigureAwait(false);
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