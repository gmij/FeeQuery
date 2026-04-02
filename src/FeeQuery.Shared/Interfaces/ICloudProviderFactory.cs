namespace FeeQuery.Shared.Interfaces;

/// <summary>
/// 云厂商工厂接口
/// 用于根据厂商代码获取对应的厂商实现
/// </summary>
public interface ICloudProviderFactory
{
    /// <summary>
    /// 根据厂商代码获取厂商实现
    /// </summary>
    /// <param name="providerCode">厂商代码（如 "alibaba", "tencent"）</param>
    /// <returns>厂商实现，如果不存在则返回null</returns>
    ICloudProvider? GetProvider(string providerCode);

    /// <summary>
    /// 获取所有已注册的厂商
    /// </summary>
    /// <returns>厂商列表</returns>
    IEnumerable<ICloudProvider> GetAllProviders();

    /// <summary>
    /// 检查是否支持指定厂商
    /// </summary>
    /// <param name="providerCode">厂商代码</param>
    /// <returns>是否支持</returns>
    bool IsProviderSupported(string providerCode);
}
