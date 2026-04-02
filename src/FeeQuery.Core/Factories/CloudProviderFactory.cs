using FeeQuery.Shared.Interfaces;

namespace FeeQuery.Core.Factories;

/// <summary>
/// 云厂商工厂实现
/// 使用依赖注入管理所有厂商实例
/// </summary>
public class CloudProviderFactory : ICloudProviderFactory
{
    private readonly IEnumerable<ICloudProvider> _providers;
    private readonly Dictionary<string, ICloudProvider> _providerMap;

    public CloudProviderFactory(IEnumerable<ICloudProvider> providers)
    {
        _providers = providers;
        // 构建厂商代码到实例的映射
        _providerMap = _providers.ToDictionary(p => p.ProviderCode, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 根据厂商代码获取厂商实现
    /// </summary>
    public ICloudProvider? GetProvider(string providerCode)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
        {
            return null;
        }

        return _providerMap.TryGetValue(providerCode, out var provider) ? provider : null;
    }

    /// <summary>
    /// 获取所有已注册的厂商
    /// </summary>
    public IEnumerable<ICloudProvider> GetAllProviders()
    {
        return _providers;
    }

    /// <summary>
    /// 检查是否支持指定厂商
    /// </summary>
    public bool IsProviderSupported(string providerCode)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
        {
            return false;
        }

        return _providerMap.ContainsKey(providerCode);
    }
}
