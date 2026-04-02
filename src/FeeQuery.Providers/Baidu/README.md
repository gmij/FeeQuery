# 百度云费用查询适配器

## 概述
百度云（Baidu Cloud）费用查询适配器，使用BCE签名算法实现API认证。

## 获取凭证

### 1. 登录百度云控制台
访问：https://console.bce.baidu.com/

### 2. 创建Access Key
1. 点击右上角用户名，选择"安全认证"
2. 进入"Access Key管理"页面
3. 点击"创建Access Key"
4. 保存生成的 **Access Key ID** 和 **Secret Access Key**

⚠️ **重要提示**：
- Secret Access Key只在创建时显示一次，请妥善保存
- 建议为API调用创建独立的子用户，并分配最小权限
- 定期轮换Access Key以提高安全性

## 所需权限

确保Access Key具有以下权限：
- **账单查询权限**：查询账单明细和余额
- **费用中心访问权限**：访问费用相关API

推荐使用RAM子用户，并分配 `BillingReadOnlyAccess` 策略。

## API文档

- 计费中心API文档：https://cloud.baidu.com/doc/BILLING/s/Gjz5wfh8l
- BCE签名算法：https://cloud.baidu.com/doc/Reference/s/Njwvz1yfu

## 支持的功能

- ✅ 账户余额查询
- ✅ 现金余额和信用额度分离显示
- ✅ 账单明细查询（按月）
- ✅ 服务类别分类（计算、存储、网络等）

## 常见问题

### Q: 如何测试凭证是否有效？
A: 在FeeQuery系统中添加百度云账号时，系统会自动调用余额查询API验证凭证。

### Q: 支持哪些账单查询粒度？
A: 当前支持按月查询账单明细，可以查询任意月份的账单数据。

### Q: 金额单位是什么？
A: 所有金额统一使用人民币（CNY），单位为元。
