using FeeQuery.Data.Repositories;
using FeeQuery.Shared.Interfaces;
using FeeQuery.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Core.Services;

/// <summary>
/// 通知服务
/// </summary>
public class NotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<INotificationProvider> _providers;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork unitOfWork,
        IEnumerable<INotificationProvider> providers,
        ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _providers = providers;
        _logger = logger;
    }

    /// <summary>
    /// 发送通知
    /// </summary>
    public async Task<bool> SendNotificationAsync(
        string channelType,
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 获取通知配置
            var config = await _unitOfWork.NotificationConfigs
                .GetDefaultConfigByTypeAsync(channelType, cancellationToken);

            if (config == null || !config.IsEnabled)
            {
                _logger.LogWarning("未找到启用的通知配置: {ChannelType}", channelType);
                return false;
            }

            // 获取对应的通知提供者
            var provider = _providers.FirstOrDefault(p => p.ProviderType == channelType);
            if (provider == null)
            {
                _logger.LogError("未找到通知提供者: {ChannelType}", channelType);
                return false;
            }

            // 发送通知
            var success = await provider.SendAsync(title, content, config.ConfigJson, cancellationToken);

            if (success)
            {
                _logger.LogInformation("通知发送成功: {ChannelType}", channelType);
            }
            else
            {
                _logger.LogWarning("通知发送失败: {ChannelType}", channelType);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送通知时发生错误: {ChannelType}", channelType);
            return false;
        }
    }

    /// <summary>
    /// 根据通知配置ID发送通知
    /// </summary>
    public async Task<bool> SendNotificationByConfigAsync(
        int configId,
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 获取通知配置
            var config = await _unitOfWork.NotificationConfigs
                .GetByIdAsync(configId, cancellationToken);

            if (config == null || !config.IsEnabled)
            {
                _logger.LogWarning("未找到启用的通知配置: {ConfigId}", configId);
                return false;
            }

            // 获取对应的通知提供者
            var provider = _providers.FirstOrDefault(p => p.ProviderType == config.ChannelType);
            if (provider == null)
            {
                _logger.LogError("未找到通知提供者: {ChannelType}", config.ChannelType);
                return false;
            }

            // 发送通知
            var success = await provider.SendAsync(title, content, config.ConfigJson, cancellationToken);

            if (success)
            {
                _logger.LogInformation("通知发送成功: 配置 {ConfigName}({ChannelType})", config.Name, config.ChannelType);
            }
            else
            {
                _logger.LogWarning("通知发送失败: 配置 {ConfigName}({ChannelType})", config.Name, config.ChannelType);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送通知时发生错误: 配置ID {ConfigId}", configId);
            return false;
        }
    }

    /// <summary>
    /// 测试通知配置
    /// </summary>
    public async Task<bool> TestNotificationConfigAsync(int configId, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _unitOfWork.NotificationConfigs.GetByIdAsync(configId, cancellationToken);
            if (config == null)
            {
                _logger.LogWarning("通知配置不存在: {ConfigId}", configId);
                return false;
            }

            var provider = _providers.FirstOrDefault(p => p.ProviderType == config.ChannelType);
            if (provider == null)
            {
                config.LastTestAt = DateTime.UtcNow;
                config.LastTestSuccess = false;
                config.LastTestError = $"未找到通知提供者: {config.ChannelType}";
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogError("未找到通知提供者: {ChannelType}", config.ChannelType);
                return false;
            }

            bool success;
            string? errorMessage = null;

            try
            {
                // 调用提供者的测试方法，获取详细错误信息
                var result = await provider.TestConnectionAsync(config.ConfigJson, cancellationToken);
                success = result.Success;
                errorMessage = result.ErrorMessage;

                if (!success)
                {
                    _logger.LogWarning("测试通知配置失败: {ConfigId}, 错误: {Error}", configId, errorMessage);
                }
            }
            catch (Exception testEx)
            {
                success = false;
                errorMessage = $"{testEx.GetType().Name}: {testEx.Message}";
                _logger.LogError(testEx, "测试通知配置时发生异常: {ConfigId}", configId);
            }

            // 更新测试结果
            config.LastTestAt = DateTime.UtcNow;
            config.LastTestSuccess = success;
            config.LastTestError = success ? null : errorMessage;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试通知配置时发生错误: {ConfigId}", configId);
            return false;
        }
    }

    /// <summary>
    /// 获取所有通知配置
    /// </summary>
    public async Task<List<NotificationConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default)
    {
        var allConfigs = await _unitOfWork.NotificationConfigs.GetAllAsync(cancellationToken);
        return allConfigs
            .OrderBy(c => c.ChannelType)
            .ThenByDescending(c => c.IsDefault)
            .ToList();
    }

    /// <summary>
    /// 添加通知配置
    /// </summary>
    public async Task<NotificationConfig> AddConfigAsync(NotificationConfig config, CancellationToken cancellationToken = default)
    {
        // 如果设置为默认,取消同类型其他配置的默认设置
        if (config.IsDefault)
        {
            var existingDefaults = await _unitOfWork.NotificationConfigs
                .FindAsync(c => c.ChannelType == config.ChannelType && c.IsDefault, cancellationToken);

            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
            }
        }

        await _unitOfWork.NotificationConfigs.AddAsync(config, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return config;
    }

    /// <summary>
    /// 更新通知配置
    /// </summary>
    public async Task UpdateConfigAsync(NotificationConfig config, CancellationToken cancellationToken = default)
    {
        // 如果设置为默认，取消同类型其他配置的默认设置
        if (config.IsDefault)
        {
            var existingDefaults = await _unitOfWork.NotificationConfigs
                .FindAsync(c => c.ChannelType == config.ChannelType && c.IsDefault && c.Id != config.Id, cancellationToken);

            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
            }
        }

        config.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.NotificationConfigs.Update(config);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 删除通知配置
    /// </summary>
    public async Task DeleteConfigAsync(int configId, CancellationToken cancellationToken = default)
    {
        var config = await _unitOfWork.NotificationConfigs.GetByIdAsync(configId, cancellationToken);
        if (config != null)
        {
            _unitOfWork.NotificationConfigs.Remove(config);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
