using FeeQuery.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FeeQuery.Core.Services;

/// <summary>
/// 内存事件总线实现
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(IServiceScopeFactory serviceScopeFactory, ILogger<InMemoryEventBus> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    {
        var eventType = typeof(TEvent);
        _logger.LogDebug("发布事件: {EventType}", eventType.Name);

        // 创建新的作用域来解析 Scoped 服务
        using var scope = _serviceScopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        // 获取所有该事件类型的处理器
        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handlers = serviceProvider.GetServices(handlerType);

        var tasks = new List<Task>();

        foreach (var handler in handlers)
        {
            try
            {
                // 直接转换为强类型处理器，避免反射调用
                if (handler is IEventHandler<TEvent> typedHandler)
                {
                    var task = typedHandler.HandleAsync(@event!, cancellationToken);
                    tasks.Add(task);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "事件处理器执行失败: {HandlerType}", handler?.GetType().Name ?? "Unknown");
            }
        }

        // 等待所有处理器完成
        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
            _logger.LogDebug("事件 {EventType} 已被 {Count} 个处理器处理", eventType.Name, tasks.Count);
        }
        else
        {
            _logger.LogWarning("事件 {EventType} 没有注册的处理器", eventType.Name);
        }
    }
}
