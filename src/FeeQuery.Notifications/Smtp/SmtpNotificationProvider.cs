using System.Net;
using System.Net.Mail;
using System.Text.Json;
using FeeQuery.Shared.Attributes;
using FeeQuery.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Notifications.Smtp;

/// <summary>
/// SMTP 邮件通知提供者
/// </summary>
[Service(ServiceLifetime.Singleton)]
public class SmtpNotificationProvider : INotificationProvider
{
    private readonly ILogger<SmtpNotificationProvider> _logger;

    public string ProviderType => "email";

    public SmtpNotificationProvider(ILogger<SmtpNotificationProvider> logger)
    {
        _logger = logger;
    }

    public async Task<bool> SendAsync(string title, string content, string configJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = JsonSerializer.Deserialize<EmailConfig>(configJson);
            if (config == null)
            {
                _logger.LogError("邮件配置解析失败");
                return false;
            }

            using var client = new SmtpClient(config.SmtpServer, config.Port)
            {
                Credentials = new NetworkCredential(config.Username, config.Password),
                EnableSsl = config.EnableSsl
            };

            var message = new MailMessage
            {
                From = new MailAddress(config.From, "FeeQuery 费用预警系统"),
                Subject = title,
                Body = content,
                IsBodyHtml = false
            };

            // 添加收件人
            foreach (var to in config.To.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                message.To.Add(to.Trim());
            }

            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("邮件发送成功: {To}", config.To);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "邮件发送失败");
            return false;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> TestConnectionAsync(string configJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = JsonSerializer.Deserialize<EmailConfig>(configJson);
            if (config == null)
            {
                return (false, "邮件配置解析失败");
            }

            if (string.IsNullOrEmpty(config.SmtpServer))
            {
                return (false, "SMTP服务器地址不能为空");
            }

            if (string.IsNullOrEmpty(config.From))
            {
                return (false, "发件邮箱不能为空");
            }

            if (string.IsNullOrEmpty(config.To))
            {
                return (false, "收件人不能为空");
            }

            using var client = new SmtpClient(config.SmtpServer, config.Port)
            {
                Credentials = new NetworkCredential(config.Username, config.Password),
                EnableSsl = config.EnableSsl
            };

            // 发送测试邮件
            var message = new MailMessage
            {
                From = new MailAddress(config.From, "FeeQuery 费用预警系统"),
                Subject = "【测试】FeeQuery 邮件通知测试",
                Body = "这是一封测试邮件，如果您收到此邮件，说明邮件配置正确。",
                IsBodyHtml = false
            };

            foreach (var to in config.To.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                message.To.Add(to.Trim());
            }

            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("测试邮件发送成功");
            return (true, null);
        }
        catch (SmtpFailedRecipientsException ex)
        {
            _logger.LogError(ex, "收件人地址无效");
            return (false, $"收件人地址无效: {ex.Message}");
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP错误");
            return (false, $"SMTP错误: {ex.Message}");
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "邮箱地址格式错误");
            return (false, $"邮箱地址格式错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试邮件发送失败");
            return (false, $"发送失败: {ex.Message}");
        }
    }

    private class EmailConfig
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public bool EnableSsl { get; set; } = true;
    }
}
