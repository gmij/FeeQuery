using FeeQuery.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Web.Controllers;

/// <summary>
/// 钉钉 Webhook 回调控制器
/// </summary>
[ApiController]
[Route("api/dingtalk")]
public class DingTalkWebhookController : ControllerBase
{
    private readonly BalanceAlertService _alertService;
    private readonly ILogger<DingTalkWebhookController> _logger;

    public DingTalkWebhookController(
        BalanceAlertService alertService,
        ILogger<DingTalkWebhookController> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    /// <summary>
    /// 处理预警确认回调
    /// </summary>
    /// <param name="alertId">预警ID</param>
    /// <param name="action">操作类型（acknowledge=确认, resolve=解决）</param>
    [HttpGet("alert-callback")]
    public async Task<ActionResult> AlertCallback([FromQuery] long alertId, [FromQuery] string action = "acknowledge")
    {
        try
        {
            _logger.LogInformation("收到钉钉回调: AlertId={AlertId}, Action={Action}", alertId, action);

            if (action == "resolve")
            {
                await _alertService.ResolveAlertAsync(alertId, "通过钉钉确认解决");
                return Content(GetSuccessHtml("预警已解决", "该预警已成功标记为已解决状态。"), "text/html; charset=utf-8");
            }
            else
            {
                await _alertService.AcknowledgeAlertAsync(alertId, "钉钉用户", "通过钉钉确认收到");
                return Content(GetSuccessHtml("确认成功", "已确认收到预警通知。"), "text/html; charset=utf-8");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理钉钉回调失败: AlertId={AlertId}", alertId);
            return Content(GetErrorHtml("操作失败", ex.Message), "text/html; charset=utf-8");
        }
    }

    /// <summary>
    /// 生成成功页面HTML
    /// </summary>
    private string GetSuccessHtml(string title, string message)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{title}</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background-color: #f5f5f5;
        }}
        .container {{
            background: white;
            padding: 40px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            text-align: center;
            max-width: 400px;
        }}
        .icon {{
            font-size: 48px;
            margin-bottom: 16px;
        }}
        .success-icon {{
            color: #52c41a;
        }}
        h1 {{
            font-size: 24px;
            margin: 0 0 16px 0;
            color: #262626;
        }}
        p {{
            font-size: 16px;
            color: #8c8c8c;
            margin: 0;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""icon success-icon"">✓</div>
        <h1>{title}</h1>
        <p>{message}</p>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// 生成错误页面HTML
    /// </summary>
    private string GetErrorHtml(string title, string message)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{title}</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background-color: #f5f5f5;
        }}
        .container {{
            background: white;
            padding: 40px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            text-align: center;
            max-width: 400px;
        }}
        .icon {{
            font-size: 48px;
            margin-bottom: 16px;
        }}
        .error-icon {{
            color: #ff4d4f;
        }}
        h1 {{
            font-size: 24px;
            margin: 0 0 16px 0;
            color: #262626;
        }}
        p {{
            font-size: 16px;
            color: #8c8c8c;
            margin: 0;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""icon error-icon"">✕</div>
        <h1>{title}</h1>
        <p>{message}</p>
    </div>
</body>
</html>";
    }
}
