using Microsoft.Extensions.DependencyInjection;

namespace FeeQuery.Shared.Attributes;

/// <summary>
/// 服务注册特性 - 标记需要自动注册到 DI 容器的服务
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ServiceAttribute : Attribute
{
    /// <summary>
    /// 服务生命周期
    /// </summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>
    /// 服务接口类型（如果为 null，则使用类实现的第一个接口）
    /// </summary>
    public Type? ServiceType { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="lifetime">服务生命周期，默认为 Singleton</param>
    public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Lifetime = lifetime;
    }
}
