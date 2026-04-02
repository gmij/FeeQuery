using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FeeQuery.SourceGenerators;

/// <summary>
/// 源生成器：自动生成服务注册代码
/// 扫描编译时所有程序集中带 [Service] 特性的类
/// </summary>
[Generator]
public class ServiceRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 在编译完成时生成代码
        context.RegisterSourceOutput(
            context.CompilationProvider,
            (spc, compilation) => Execute(compilation, spc));
    }

    private static void Execute(Compilation compilation, SourceProductionContext context)
    {
        var serviceList = new List<ClassInfo>();

        // 扫描当前编译中的所有类型（包括引用的程序集）
        var allTypes = compilation.Assembly.Modules
            .SelectMany(m => GetAllTypes(m.GlobalNamespace))
            .Concat(compilation.References
                .Select(r => compilation.GetAssemblyOrModuleSymbol(r))
                .OfType<IAssemblySymbol>()
                .SelectMany(a => a.Modules)
                .SelectMany(m => GetAllTypes(m.GlobalNamespace)));

        foreach (var type in allTypes)
        {
            // 跳过非类类型
            if (type.TypeKind != TypeKind.Class || type.IsAbstract)
                continue;

            // 查找 Service 特性
            var serviceAttribute = type.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "ServiceAttribute");

            if (serviceAttribute is null)
                continue;

            // 获取生命周期
            var lifetime = "Singleton";
            if (serviceAttribute.ConstructorArguments.Length > 0)
            {
                var lifetimeValue = serviceAttribute.ConstructorArguments[0].Value;
                if (lifetimeValue is int lifetimeInt)
                {
                    lifetime = lifetimeInt switch
                    {
                        0 => "Singleton",
                        1 => "Scoped",
                        2 => "Transient",
                        _ => "Singleton"
                    };
                }
            }

            // 获取服务接口类型
            string? serviceType = null;
            var serviceTypeArg = serviceAttribute.NamedArguments
                .FirstOrDefault(na => na.Key == "ServiceType");

            if (serviceTypeArg.Value.Value is INamedTypeSymbol serviceTypeSymbol)
            {
                serviceType = serviceTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
            else
            {
                // 自动推断接口（包括从基类继承的接口）
                var interfaces = type.AllInterfaces;
                if (interfaces.Length > 0)
                {
                    var matchingInterface = interfaces.FirstOrDefault(i => i.Name == $"I{type.Name}");
                    if (matchingInterface is not null)
                    {
                        serviceType = matchingInterface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    }
                    else
                    {
                        var firstNonSystemInterface = interfaces.FirstOrDefault(i =>
                            !i.ToDisplayString().StartsWith("System."));
                        if (firstNonSystemInterface is not null)
                        {
                            serviceType = firstNonSystemInterface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        }
                    }
                }

                serviceType ??= type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            serviceList.Add(new ClassInfo
            {
                ClassName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ServiceType = serviceType,
                Lifetime = lifetime
            });
        }

        // 按接口分组
        var cloudProviders = serviceList.Where(c => c.ServiceType.Contains("ICloudProvider")).ToList();
        var notificationProviders = serviceList.Where(c => c.ServiceType.Contains("INotificationProvider")).ToList();
        var otherServices = serviceList.Except(cloudProviders).Except(notificationProviders).ToList();

        // 始终生成代码，即使没有发现服务（用于调试）
        var source = GenerateSource(cloudProviders, notificationProviders, otherServices);
        context.AddSource("ServiceRegistration.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            yield return type;

            // 递归处理嵌套类型
            foreach (var nestedType in GetNestedTypes(type))
            {
                yield return nestedType;
            }
        }

        // 递归处理子命名空间
        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var type in GetAllTypes(childNamespace))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nestedType in type.GetTypeMembers())
        {
            yield return nestedType;

            foreach (var doublyNestedType in GetNestedTypes(nestedType))
            {
                yield return doublyNestedType;
            }
        }
    }

    private static string GenerateSource(
        List<ClassInfo> cloudProviders,
        List<ClassInfo> notificationProviders,
        List<ClassInfo> otherServices)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine($"// 源生成器扫描结果: 云厂商={cloudProviders.Count}, 通知提供者={notificationProviders.Count}, 其他服务={otherServices.Count}");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("namespace FeeQuery.Shared.Extensions;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// 自动生成的服务注册扩展方法（源生成器）");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static partial class DependencyInjectionExtensions");
        sb.AppendLine("{");

        // 生成 AddGeneratedCloudProviders 方法（始终生成，即使为空）
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// 自动注册所有云厂商提供者（源生成器，共{cloudProviders.Count}个）");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddGeneratedCloudProviders(this IServiceCollection services)");
        sb.AppendLine("    {");
        foreach (var provider in cloudProviders)
        {
            sb.AppendLine($"        services.Add{provider.Lifetime}<{provider.ServiceType}, {provider.ClassName}>();");
        }
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // 生成 AddGeneratedNotificationProviders 方法（始终生成，即使为空）
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// 自动注册所有通知提供者（源生成器，共{notificationProviders.Count}个）");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddGeneratedNotificationProviders(this IServiceCollection services)");
        sb.AppendLine("    {");
        foreach (var provider in notificationProviders)
        {
            sb.AppendLine($"        services.Add{provider.Lifetime}<{provider.ServiceType}, {provider.ClassName}>();");
        }
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // 生成 AddAllGeneratedServices 方法
        var totalCount = cloudProviders.Count + notificationProviders.Count + otherServices.Count;
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// 自动注册所有带 [Service] 特性的服务（源生成器，共{totalCount}个）");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddAllGeneratedServices(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        services.AddGeneratedCloudProviders();");
        sb.AppendLine("        services.AddGeneratedNotificationProviders();");

        foreach (var service in otherServices)
        {
            sb.AppendLine($"        services.Add{service.Lifetime}<{service.ServiceType}, {service.ClassName}>();");
        }

        sb.AppendLine("        return services;");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    private class ClassInfo
    {
        public string ClassName { get; set; } = "";
        public string ServiceType { get; set; } = "";
        public string Lifetime { get; set; } = "";
    }
}
