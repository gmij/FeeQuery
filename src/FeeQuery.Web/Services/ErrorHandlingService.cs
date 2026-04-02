using Microsoft.Extensions.Logging;

namespace FeeQuery.Web.Services;

/// <summary>
/// 统一错误处理服务
/// </summary>
public class ErrorHandlingService
{
    private readonly ILogger<ErrorHandlingService> _logger;

    public ErrorHandlingService(ILogger<ErrorHandlingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 处理错误并返回用户友好的错误消息
    /// </summary>
    public string HandleError(Exception ex, string operation = "操作")
    {
        var errorMessage = GetUserFriendlyMessage(ex, operation);
        _logger.LogError(ex, "{Operation}失败: {Message}", operation, ex.Message);
        return errorMessage;
    }

    /// <summary>
    /// 获取用户友好的错误消息
    /// </summary>
    private string GetUserFriendlyMessage(Exception ex, string operation)
    {
        return ex switch
        {
            InvalidOperationException => $"{operation}失败：{ex.Message}",
            ArgumentException => $"参数错误：{ex.Message}",
            UnauthorizedAccessException => "您没有权限执行此操作",
            TimeoutException => $"{operation}超时，请稍后重试",
            HttpRequestException => "网络请求失败，请检查网络连接",
            System.Data.Common.DbException => $"数据库操作失败：{ex.Message}",
            _ => $"{operation}失败：{ex.Message}"
        };
    }

    /// <summary>
    /// 记录警告
    /// </summary>
    public void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning(message, args);
    }

    /// <summary>
    /// 记录信息
    /// </summary>
    public void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation(message, args);
    }
}
