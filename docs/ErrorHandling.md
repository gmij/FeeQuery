# 错误处理最佳实践

## 概述

FeeQuery 项目使用统一的错误处理机制，确保所有错误都能在用户界面上显示友好的提示信息，同时在后台记录详细的日志。

## 核心组件

### 1. ErrorHandlingService

位置：`src/FeeQuery.Web/Services/ErrorHandlingService.cs`

**功能：**
- 统一处理异常并返回用户友好的错误消息
- 自动记录错误日志
- 根据异常类型返回不同的错误提示

**使用示例：**
```csharp
var errorMessage = ErrorHandler.HandleError(ex, "操作名称");
```

### 2. BasePage 组件

位置：`src/FeeQuery.Web/Components/Pages/BasePage.cs`

**功能：**
- 所有页面的基类
- 提供统一的错误处理方法
- 集成 AntBlazor 的 Message 服务

**主要方法：**

```csharp
// 执行操作并自动处理错误
await ExecuteAsync(async () =>
{
    // 你的业务逻辑
}, "操作名称");

// 执行操作并返回结果
var result = await ExecuteAsync<T>(async () =>
{
    return await SomeOperation();
}, "操作名称", defaultValue);

// 显示消息
await ShowSuccess("操作成功");
await ShowError("操作失败");
await ShowWarning("警告信息");
await ShowInfo("提示信息");
```

### 3. GlobalErrorBoundary 组件

位置：`src/FeeQuery.Web/Components/Layout/GlobalErrorBoundary.razor`

**功能：**
- 捕获全局未处理的异常
- 显示友好的错误界面
- 提供页面恢复功能

## 使用指南

### 步骤1：继承 BasePage

```razor
@page "/your-page"
@inherits BasePage
@inject YourService Service
```

### 步骤2：使用 ExecuteAsync 包装操作

**错误示例（不推荐）：**
```csharp
private async Task DeleteItem(int id)
{
    try
    {
        await Service.DeleteAsync(id);
        Message.Success("删除成功");  // ❌ 直接使用 Message
    }
    catch (Exception ex)
    {
        Message.Error($"删除失败: {ex.Message}");  // ❌ 手动处理错误
    }
}
```

**正确示例（推荐）：**
```csharp
private async Task DeleteItem(int id)
{
    await ExecuteAsync(async () =>
    {
        await Service.DeleteAsync(id);
        await ShowSuccess("删除成功");  // ✅ 使用 ShowSuccess
    }, "删除项目");  // ✅ 提供操作名称
}
```

### 步骤3：处理多个操作的错误

```csharp
private async Task RefreshAll()
{
    refreshing = true;
    try
    {
        await ExecuteAsync(async () =>
        {
            var items = await GetItems();

            foreach (var item in items)
            {
                try
                {
                    await RefreshItem(item.Id);
                }
                catch (Exception ex)
                {
                    // 单个项目失败不影响其他项目
                    await ShowError($"刷新 {item.Name} 失败：{ex.Message}");
                }
            }

            await ShowSuccess("刷新完成");
        }, "刷新所有项目");
    }
    finally
    {
        refreshing = false;
    }
}
```

## 错误消息规范

### 1. 操作名称命名规范

- 使用动词+名词：`"添加账号"`、`"删除配置"`、`"刷新余额"`
- 简洁明确，2-4个字
- 用户理解的语言，避免技术术语

### 2. 成功消息

- 简短肯定：`"保存成功"`、`"删除成功"`
- 可选提供详情：`"账号添加成功，余额初始化中..."`

### 3. 错误消息

- **自动生成**：`ExecuteAsync` 会根据异常类型自动生成
- **手动指定**：特殊情况下使用 `await ShowError("具体错误")`
- **格式**：`"{操作}失败：{原因}"`

### 4. 警告消息

用于非错误但需要注意的情况：
```csharp
if (string.IsNullOrEmpty(config.Name))
{
    await ShowWarning("请输入配置名称");
    return;
}
```

## 常见模式

### 模式1：简单的CRUD操作

```csharp
private async Task SaveItem()
{
    await ExecuteAsync(async () =>
    {
        await Service.SaveAsync(model);
        await ShowSuccess("保存成功");
        await OnSuccess.InvokeAsync();
    }, "保存");
}
```

### 模式2：需要验证的操作

```csharp
private async Task SubmitForm()
{
    if (string.IsNullOrEmpty(model.Name))
    {
        await ShowWarning("请输入名称");
        return;
    }

    await ExecuteAsync(async () =>
    {
        await Service.SubmitAsync(model);
        await ShowSuccess("提交成功");
    }, "提交表单");
}
```

### 模式3：批量操作

```csharp
private async Task BatchDelete(List<int> ids)
{
    await ExecuteAsync(async () =>
    {
        var successCount = 0;
        var failedCount = 0;

        foreach (var id in ids)
        {
            try
            {
                await Service.DeleteAsync(id);
                successCount++;
            }
            catch
            {
                failedCount++;
            }
        }

        if (failedCount == 0)
        {
            await ShowSuccess($"成功删除 {successCount} 项");
        }
        else
        {
            await ShowWarning($"成功 {successCount} 项，失败 {failedCount} 项");
        }
    }, "批量删除");
}
```

### 模式4：带Loading状态的操作

```csharp
private async Task LongRunningOperation()
{
    loading = true;
    try
    {
        await ExecuteAsync(async () =>
        {
            await Service.ProcessAsync();
            await LoadData();  // 重新加载数据
            await ShowSuccess("操作完成");
        }, "处理数据");
    }
    finally
    {
        loading = false;
    }
}
```

## 异常类型处理

`ErrorHandlingService` 会根据异常类型返回不同的消息：

| 异常类型 | 错误消息格式 |
|---------|------------|
| `InvalidOperationException` | `"{操作}失败：{异常消息}"` |
| `ArgumentException` | `"参数错误：{异常消息}"` |
| `UnauthorizedAccessException` | `"您没有权限执行此操作"` |
| `TimeoutException` | `"{操作}超时，请稍后重试"` |
| `HttpRequestException` | `"网络请求失败，请检查网络连接"` |
| `DbException` | `"数据库操作失败：{异常消息}"` |
| 其他异常 | `"{操作}失败：{异常消息}"` |

## 日志记录

所有通过 `ErrorHandlingService` 处理的错误都会自动记录日志：

```
[Error] 删除账号失败: System.InvalidOperationException: 账号不存在
```

无需手动调用 `ILogger`，除非有特殊需求。

## 迁移现有代码

### 步骤1：添加 @inherits BasePage

```diff
@page "/your-page"
@rendermode InteractiveServer
+ @inherits BasePage
@inject YourService Service
- @inject IMessageService Message
```

### 步骤2：替换 try-catch

```diff
- try
- {
-     await Service.DoSomething();
-     Message.Success("成功");
- }
- catch (Exception ex)
- {
-     Message.Error($"失败: {ex.Message}");
- }

+ await ExecuteAsync(async () =>
+ {
+     await Service.DoSomething();
+     await ShowSuccess("成功");
+ }, "操作名称");
```

### 步骤3：测试验证

运行应用并测试：
1. 正常操作是否显示成功消息
2. 错误操作是否显示友好错误消息
3. 错误是否被记录到日志

## 注意事项

1. **不要吞掉异常**：让 `ExecuteAsync` 处理，不要空catch
2. **提供操作名称**：帮助用户理解哪个操作失败了
3. **避免技术细节**：错误消息面向最终用户
4. **使用async/await**：所有消息方法都是异步的
5. **Loading状态管理**：在 finally 块中重置 loading 状态

## 示例页面

参考以下已迁移的页面：
- `src/FeeQuery.Web/Components/Pages/BalanceDashboard.razor`
- `src/FeeQuery.Web/Components/Pages/Accounts.razor`
