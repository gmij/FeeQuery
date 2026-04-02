# 火山引擎适配器使用说明

## 概述

火山引擎适配器基于火山引擎OpenAPI实现，使用签名算法V4进行API认证和调用。

## 技术实现

### API端点和服务信息

- **服务名称**: `billing`
- **API端点**: `https://open.volcengineapi.com`
- **API版本**: `2022-01-01`
- **区域**: `cn-north-1`

### 签名算法V4实现

火山引擎使用类似AWS签名V4的算法，包括以下步骤：

1. **构建规范请求 (Canonical Request)**
   ```
   POST
   /
   (空查询字符串)
   content-type:application/json
   host:open.volcengineapi.com
   x-date:(当前UTC时间)

   content-type;host;x-date
   (请求体SHA256哈希)
   ```

2. **构建待签名字符串 (String to Sign)**
   ```
   HMAC-SHA256
   (ISO 8601时间戳)
   (日期)/(区域)/(服务名)/request
   (规范请求的SHA256哈希)
   ```

3. **计算签名密钥 (Signing Key)**
   ```csharp
   kDate = HMAC-SHA256("VOLC" + SecretAccessKey, DateStamp)
   kRegion = HMAC-SHA256(kDate, Region)
   kService = HMAC-SHA256(kRegion, ServiceName)
   kSigning = HMAC-SHA256(kService, "request")
   ```

4. **生成签名 (Signature)**
   ```csharp
   signature = Hex(HMAC-SHA256(kSigning, StringToSign))
   ```

5. **构建Authorization头**
   ```
   HMAC-SHA256 Credential=(AccessKeyId)/(CredentialScope), SignedHeaders=(SignedHeaders), Signature=(Signature)
   ```

## API接口

### 1. ListBillDetail - 查询账单明细

**用途**: 按月查询详细账单记录

**请求参数**:
```json
{
  "Action": "ListBillDetail",
  "Version": "2022-01-01",
  "BillPeriod": "2024-12",  // 账单月份，格式：yyyy-MM
  "Limit": 100,              // 每页记录数
  "Offset": 0                // 偏移量
}
```

**响应字段**:
- `ProductName`: 产品名称
- `PayableAmount`: 应付金额
- `UsageQuantity`: 用量
- `Unit`: 单位
- `Region`: 区域
- `BillPeriod`: 账期

### 2. QueryAccountBalance - 查询账户余额

**用途**: 查询当前账户可用余额和信用额度

**请求参数**:
```json
{
  "Action": "QueryAccountBalance",
  "Version": "2022-01-01"
}
```

**响应字段**:
- `AvailableBalance`: 可用余额
- `CreditLimit`: 信用额度

## 凭证配置

火山引擎需要以下凭证：

1. **AccessKeyId** (必填)
   - 火山引擎AK（Access Key ID）
   - 用于标识API调用者身份
   - 非敏感信息

2. **SecretAccessKey** (必填)
   - 火山引擎SK（Secret Access Key）
   - 用于签名验证
   - 敏感信息，需加密存储

## 功能特性

### ✅ 已实现

1. **完整的签名算法V4实现**
   - SHA256哈希计算
   - HMAC-SHA256签名
   - 规范请求构建
   - Authorization头生成

2. **账单查询功能**
   - 支持按月查询
   - 支持分页获取（自动处理）
   - 数据去重和日期过滤
   - 服务分类映射

3. **凭证验证**
   - 通过实际API调用验证
   - 返回详细的错误日志

4. **异常处理和日志**
   - 完整的异常捕获
   - 使用ILogger记录调试信息
   - HTTP错误详细输出

### 🎯 特点

- **无需官方SDK**: 火山引擎没有官方.NET SDK，本实现完全基于HTTP客户端
- **标准化接口**: 实现了统一的`ICloudProvider`接口
- **自动重试**: 支持分页自动获取所有数据
- **服务映射**: 智能映射火山引擎服务到标准分类

## 服务分类映射规则

| 火山引擎服务 | 分类 |
|------------|------|
| ECS、云服务器、实例 | 计算 |
| TOS、对象存储、Storage | 存储 |
| VPC、EIP、网络、带宽 | 网络 |
| RDS、MongoDB、Redis、数据库 | 数据库 |
| CDN、内容分发 | CDN |
| ML、AI、智能、机器学习 | AI服务 |
| 安全、防护 | 安全 |
| 其他 | 其他 |

## 使用示例

```csharp
// 1. 准备凭证
var credentials = new CloudCredentials();
credentials.SetCredential("AccessKeyId", "your-access-key-id");
credentials.SetCredential("SecretAccessKey", "your-secret-access-key");

// 2. 验证凭证
var provider = new VolcanoCloudProvider(httpClientFactory, logger);
var isValid = await provider.ValidateCredentialsAsync(credentials);

// 3. 查询账单
var startDate = new DateTime(2024, 11, 1);
var endDate = new DateTime(2024, 12, 31);
var records = await provider.GetBillingDataAsync(
    cloudAccount,
    credentials,
    startDate,
    endDate);

// 4. 查询余额
var balance = await provider.GetAccountBalanceAsync(credentials);
Console.WriteLine($"可用余额: {balance.AvailableBalance} {balance.Currency}");
```

## 注意事项

### API限制

1. **时间格式**: 所有时间使用UTC时间
2. **日期格式**: BillPeriod使用`yyyy-MM`格式
3. **分页限制**: 建议每页不超过100条记录

### 最佳实践

1. **凭证安全**:
   - 使用Data Protection API加密存储凭证
   - 不要在日志中输出SecretAccessKey

2. **错误处理**:
   - 捕获并记录所有API异常
   - 单个月份失败不影响其他月份查询

3. **性能优化**:
   - 使用HttpClientFactory管理HTTP客户端
   - 合理设置分页大小
   - 避免频繁调用API

## 参考文档

- [火山引擎费用中心API概览](https://www.volcengine.com/docs/6269/1165275)
- [火山引擎OpenAPI调用说明](https://www.volcengine.com/docs/6269/130258)
- [火山引擎签名方法](https://www.volcengine.com/docs/6369/67269)

## 版本历史

- **v1.0** (2024-12-08)
  - 初始版本
  - 实现签名算法V4
  - 支持账单查询和余额查询
  - 完整的错误处理和日志记录
