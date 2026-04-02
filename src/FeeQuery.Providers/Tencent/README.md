# 腾讯云费用查询适配器

## 概述
腾讯云（Tencent Cloud）费用查询适配器，使用API 3.0 TC3-HMAC-SHA256签名算法实现。

## 获取凭证

### 1. 登录腾讯云控制台
访问：https://console.cloud.tencent.com/

### 2. 创建API密钥
1. 点击右上角用户名，选择"访问管理"
2. 进入"API密钥管理"页面：https://console.cloud.tencent.com/cam/capi
3. 点击"新建密钥"
4. 保存生成的 **SecretId** 和 **SecretKey**

⚠️ **重要提示**：
- SecretKey只在创建时显示一次，请妥善保存
- 建议为API调用创建独立的子用户，并分配最小权限
- 定期轮换API密钥以提高安全性

## 所需权限

确保API密钥具有以下权限：
- **计费相关权限**：`QcloudFinanceFullAccess` 或 `QcloudFinanceReadOnlyAccess`
- **账单查询权限**：访问计费中心API

推荐使用CAM子用户，并分配 `QcloudFinanceReadOnlyAccess` 策略（只读权限）。

## API文档

- 计费相关API：https://cloud.tencent.com/document/api/555/19182
- 账户余额查询：https://cloud.tencent.com/document/api/555/20253
- API签名方法：https://cloud.tencent.com/document/api/555/19183

## 支持的功能

- ✅ 账户余额查询
- ✅ 现金余额、代金券、信用额度分离显示
- ✅ 账单明细查询（按月）
- ✅ 服务类别分类（计算、存储、网络等）
- ✅ 多区域支持（默认：广州）

## 特殊说明

### 金额单位
腾讯云API返回的金额单位是**分**，适配器会自动转换为**元**。

### 余额字段说明
- **Balance**：账户总余额（包含现金和代金券）
- **RealBalance**：现金余额
- **CashAccountBalance**：代金券余额
- **Credit**：信用额度

适配器会自动解析这些字段并正确显示。

## 常见问题

### Q: 如何测试凭证是否有效？
A: 在FeeQuery系统中添加腾讯云账号时，系统会自动调用`DescribeAccountBalance` API验证凭证。

### Q: 支持哪些账单查询粒度？
A: 当前支持按月查询账单明细，可以查询任意月份的账单数据。

### Q: 为什么余额显示为负数？
A: 如果账户有欠费，余额可能为负数。这是正常现象，请及时充值。

### Q: 如何查看更详细的账单？
A: 访问腾讯云费用中心：https://console.cloud.tencent.com/expense/overview
