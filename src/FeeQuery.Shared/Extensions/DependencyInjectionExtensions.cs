using System.Reflection;
using FeeQuery.Shared.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Shared.Extensions;

/// <summary>
/// 依赖注入扩展方法
/// </summary>
public static partial class DependencyInjectionExtensions
{
    /// <summary>
    /// 自动发现并注册指定程序集中标记了 [Service] 特性的服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="assemblies">要扫描的程序集</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddAutoDiscoveredServices(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }

        var discoveredServices = new List<(Type ServiceType, Type ImplementationType, ServiceLifetime Lifetime)>();

        foreach (var assembly in assemblies)
        {
            try
            {
                // 跳过系统程序集和第三方程序集
                if (assembly.FullName?.StartsWith("System") == true ||
                    assembly.FullName?.StartsWith("Microsoft") == true ||
                    assembly.FullName?.StartsWith("netstandard") == true)
                {
                    continue;
                }

                var types = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<ServiceAttribute>() != null);

                foreach (var implementationType in types)
                {
                    var attribute = implementationType.GetCustomAttribute<ServiceAttribute>()!;

                    // 确定服务接口类型
                    Type serviceType;
                    if (attribute.ServiceType != null)
                    {
                        serviceType = attribute.ServiceType;
                    }
                    else
                    {
                        // 使用类实现的第一个接口（排除系统接口）
                        var interfaces = implementationType.GetInterfaces()
                            .Where(i => !i.FullName?.StartsWith("System") == true)
                            .ToArray();

                        if (interfaces.Length == 0)
                        {
                            // 如果没有接口，直接注册实现类
                            serviceType = implementationType;
                        }
                        else
                        {
                            // 优先使用与实现类同名的接口（如 FooService -> IFooService）
                            serviceType = interfaces.FirstOrDefault(i => i.Name == $"I{implementationType.Name}")
                                ?? interfaces.First();
                        }
                    }

                    discoveredServices.Add((serviceType, implementationType, attribute.Lifetime));
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                // 记录加载失败的类型
                Console.WriteLine($"Warning: Failed to load types from assembly {assembly.FullName}: {ex.Message}");
            }
        }

        // 注册发现的服务
        foreach (var (serviceType, implementationType, lifetime) in discoveredServices)
        {
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    services.AddSingleton(serviceType, implementationType);
                    break;
                case ServiceLifetime.Scoped:
                    services.AddScoped(serviceType, implementationType);
                    break;
                case ServiceLifetime.Transient:
                    services.AddTransient(serviceType, implementationType);
                    break;
            }
        }

        return services;
    }

    /// <summary>
    /// 自动发现并注册实现了指定接口的所有服务
    /// </summary>
    /// <typeparam name="TInterface">服务接口类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="lifetime">服务生命周期，默认为 Singleton</param>
    /// <param name="assemblies">要扫描的程序集（如果为空，扫描所有已加载的程序集）</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddImplementationsOf<TInterface>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }

        var interfaceType = typeof(TInterface);
        var implementations = new List<Type>();

        foreach (var assembly in assemblies)
        {
            try
            {
                // 跳过系统程序集
                if (assembly.FullName?.StartsWith("System") == true ||
                    assembly.FullName?.StartsWith("Microsoft") == true ||
                    assembly.FullName?.StartsWith("netstandard") == true)
                {
                    continue;
                }

                var types = assembly.GetTypes()
                    .Where(t => t.IsClass &&
                               !t.IsAbstract &&
                               interfaceType.IsAssignableFrom(t) &&
                               t != interfaceType);

                implementations.AddRange(types);
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine($"Warning: Failed to load types from assembly {assembly.FullName}: {ex.Message}");
            }
        }

        // 注册所有实现
        foreach (var implementationType in implementations)
        {
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    services.AddSingleton(interfaceType, implementationType);
                    break;
                case ServiceLifetime.Scoped:
                    services.AddScoped(interfaceType, implementationType);
                    break;
                case ServiceLifetime.Transient:
                    services.AddTransient(interfaceType, implementationType);
                    break;
            }
        }

        return services;
    }

    /// <summary>
    /// 自动发现并注册所有云厂商提供者
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="assemblies">要扫描的程序集（如果为空，扫描所有已加载的程序集）</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddCloudProviders(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        return services.AddImplementationsOf<Interfaces.ICloudProvider>(ServiceLifetime.Singleton, assemblies);
    }

    /// <summary>
    /// 自动发现并注册所有通知提供者
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="assemblies">要扫描的程序集（如果为空，扫描所有已加载的程序集）</param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddNotificationProviders(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        return services.AddImplementationsOf<Interfaces.INotificationProvider>(ServiceLifetime.Singleton, assemblies);
    }

    /// <summary>
    /// 从指定命名空间前缀加载程序集
    /// </summary>
    /// <param name="namespacePrefix">命名空间前缀（如 "FeeQuery.Providers"）</param>
    /// <returns>匹配的程序集数组</returns>
    public static Assembly[] LoadAssembliesFromNamespace(string namespacePrefix)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.StartsWith(namespacePrefix) == true)
            .ToArray();
    }

    /// <summary>
    /// 记录已注册的服务到日志
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="serviceTypeFilter">服务类型过滤器（可选）</param>
    public static void LogRegisteredServices(
        this IServiceCollection services,
        ILogger logger,
        Func<Type, bool>? serviceTypeFilter = null)
    {
        var registeredServices = services
            .Where(s => serviceTypeFilter == null || serviceTypeFilter(s.ServiceType))
            .GroupBy(s => s.Lifetime)
            .OrderBy(g => g.Key);

        foreach (var group in registeredServices)
        {
            logger.LogInformation("【{Lifetime}】服务 ({Count} 个):", group.Key, group.Count());
            foreach (var service in group)
            {
                var implementationType = service.ImplementationType?.Name
                    ?? service.ImplementationInstance?.GetType().Name
                    ?? service.ImplementationFactory?.Method.ReturnType.Name
                    ?? "Unknown";

                logger.LogInformation("  - {ServiceType} -> {ImplementationType}",
                    service.ServiceType.Name, implementationType);
            }
        }
    }
}
