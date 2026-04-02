using AntDesign;
using Microsoft.AspNetCore.Components;

namespace FeeQuery.Web.Components.Pages;

/// <summary>
/// 基础页面组件，提供统一的错误处理
/// </summary>
public class BasePage : ComponentBase
{
    [Inject]
    protected IMessageService Message { get; set; } = default!;

    [Inject]
    protected FeeQuery.Web.Services.ErrorHandlingService ErrorHandler { get; set; } = default!;

    /// <summary>
    /// 执行操作并处理错误
    /// </summary>
    protected async Task ExecuteAsync(Func<Task> action, string operationName = "操作")
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            var errorMessage = ErrorHandler.HandleError(ex, operationName);
            await Message.ErrorAsync(errorMessage);
        }
    }

    /// <summary>
    /// 执行操作并处理错误（带返回值）
    /// </summary>
    protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> action, string operationName = "操作", T? defaultValue = default)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            var errorMessage = ErrorHandler.HandleError(ex, operationName);
            await Message.ErrorAsync(errorMessage);
            return defaultValue;
        }
    }

    /// <summary>
    /// 显示成功消息
    /// </summary>
    protected async Task ShowSuccess(string message)
    {
        await Message.SuccessAsync(message);
    }

    /// <summary>
    /// 显示警告消息
    /// </summary>
    protected async Task ShowWarning(string message)
    {
        await Message.WarningAsync(message);
        ErrorHandler.LogWarning(message);
    }

    /// <summary>
    /// 显示错误消息
    /// </summary>
    protected async Task ShowError(string message)
    {
        await Message.ErrorAsync(message);
        ErrorHandler.LogWarning("用户收到错误消息: {Message}", message);
    }

    /// <summary>
    /// 显示信息消息
    /// </summary>
    protected async Task ShowInfo(string message)
    {
        await Message.InfoAsync(message);
    }
}
