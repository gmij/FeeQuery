# MiniMax余额透支显示修复

## 问题描述

MiniMax账户出现以下异常情况：
- 现金余额：0 元
- 代金券余额：0 元
- 信用额度：5000 元
- 总可用额度（API返回）：4840.83 元

实际情况是账户已经透支了 159.17 元（5000 - 4840.83），但界面仍显示"总可用额度 5000 元"，未明确标识欠费状态。

## 根本原因

MiniMax API 的 `available_amount` 字段计算逻辑：
```
available_amount = cash_balance + voucher_balance + 剩余可用信用额度
```

而不是：
```
available_amount = cash_balance + voucher_balance + 信用额度总量
```

当现金用完后，`available_amount` 实际返回的是剩余可用信用额度，而非信用额度总量。

## 修复方案

### 1. 修改 MiniMaxProvider.cs 的余额计算逻辑

**文件**: [src/FeeQuery.Providers/MiniMax/MiniMaxProvider.cs](../src/FeeQuery.Providers/MiniMax/MiniMaxProvider.cs#L109-L145)

**修改内容**:
- 当 `cash_balance + voucher_balance = 0` 且 `credit_balance > 0` 时，判定为透支状态
- 计算已透支金额：`usedCredit = creditLimit - totalAvailable`
- 设置 `AvailableBalance` 为负数表示透支：`AvailableBalance = -usedCredit`
- 添加透支警告日志

### 2. 扩展 AccountBalance 模型

**文件**: [src/FeeQuery.Shared/Models/AccountBalance.cs](../src/FeeQuery.Shared/Models/AccountBalance.cs)

**新增属性**:
- `IsOverdrawn` (bool): 是否已透支（当 AvailableBalance < 0）
- `OverdrawnAmount` (decimal): 已透支金额的绝对值
- `TotalAvailable` (decimal): 实际可用总额度（根据是否透支自动计算）

**计算逻辑**:
```csharp
// 已透支情况
TotalAvailable = CreditLimit - OverdrawnAmount

// 未透支情况
TotalAvailable = AvailableBalance + CreditLimit
```

### 3. 更新界面显示逻辑

**文件**: [src/FeeQuery.Web/Components/Pages/BalanceDashboard.razor](../src/FeeQuery.Web/Components/Pages/BalanceDashboard.razor#L40-L119)

**设计原则**:
- 保持布局一致性：透支和未透支情况使用相同的左右两列布局
- 警告图标：标题前显示红色 exclamation-circle 图标
- 视觉层次：通过颜色和图标区分不同状态

**透支时显示**:
- 标题图标：🔴 红色警告图标
- 左列：已透支金额（红色加粗）
- 右列：剩余可用信用（根据预警规则显示绿色/红色）
- 底部：信用额度总量（灰色）

**未透支时显示**:
- 标题图标：无
- 左列：现金余额
- 右列：信用额度
- 底部：总可用额度（蓝色）

## 测试方法

### 1. 启动应用

```bash
dotnet run --project src/FeeQuery.Web
```

### 2. 访问余额看板

打开浏览器访问: `http://localhost:5000/balance-dashboard`

### 3. 刷新 MiniMax 账户余额

点击 MiniMax 账户卡片上的"立即刷新"按钮，等待刷新完成。

### 4. 验证显示效果

**预期结果（已透支情况）**:
- 标题显示：🔴 **福宇minimax**（红色警告图标）
- 已透支金额：**159.44 元**（红色加粗）
- 剩余可用信用：**4,840.56 元**
- 信用额度总量：5,000.00 元（灰色）

**视觉效果对比**:

```
┌─────────────────────────────────────────────┐
│ 🔴 福宇minimax              [MiniMax 标签]   │  ← 红色警告图标
├─────────────────────────────────────────────┤
│                                             │
│  已透支金额           剩余可用信用             │
│  159.44 元           4,840.56 元            │
│  (红色加粗)           (绿色/红色)             │
│                                             │
│  ─────────────────────────────────────      │
│                                             │
│  信用额度总量                                 │
│  5,000.00 元                                │
│  (灰色)                                      │
│                                             │
│  最后同步: 01-12 10:38                       │
│  [查看历史] [立即刷新]                        │
└─────────────────────────────────────────────┘
```

**未透支情况对比**:

```
┌─────────────────────────────────────────────┐
│ 福宇火山               [火山云 标签]          │  ← 无警告图标
├─────────────────────────────────────────────┤
│                                             │
│  现金余额             信用额度                │
│  1,440.01 元         3,000.00 元            │
│  (绿色)               (默认色)               │
│                                             │
│  ───────────────────────────────────────      │
│                                             │
│  总可用额度                                   │
│  4,440.01 元                                │
│  (蓝色)                                      │
│                                             │
│  最后同步: 01-12 10:15                       │
│  [查看历史] [立即刷新]                        │
└─────────────────────────────────────────────┘
```

**日志输出**:
```
warn: FeeQuery.Providers.MiniMax.MiniMaxProvider[0]
      MiniMax账户已透支信用额度: 现金=0, 代金券=0, 信用额度总量=5000,
      已用信用=159.17, 剩余可用信用=4840.83, 透支金额=159.17
```

### 5. 测试其他场景

**场景 A：有现金余额，未使用信用**
- 现金: 1000 元
- 信用额度: 5000 元
- 显示: 现金余额 1000 元，信用额度 5000 元，总可用 6000 元

**场景 B：无信用额度的账户**
- 现金: 500 元
- 信用额度: null
- 显示: 可用余额 500 元

**场景 C：完全透支（剩余可用为 0）**
- 现金: 0 元
- 信用额度: 5000 元
- 总可用: 0 元
- 显示: 已透支 5000 元，剩余可用信用 0 元（红色警告）

## 数据流程

```
MiniMax API
  ↓
  available_amount = 4840.83
  cash_balance = 0
  voucher_balance = 0
  credit_balance = 5000
  ↓
MiniMaxProvider (计算)
  ↓
  检测到透支: cashAndVoucher = 0, creditLimit = 5000
  已用信用 = 5000 - 4840.83 = 159.17
  AvailableBalance = -159.17 (负数表示透支)
  CreditLimit = 5000
  ↓
AccountBalance 模型
  ↓
  IsOverdrawn = true (因为 AvailableBalance < 0)
  OverdrawnAmount = 159.17 (绝对值)
  TotalAvailable = 5000 - 159.17 = 4840.83
  ↓
BalanceDashboard 界面
  ↓
  显示红色警告 + 透支金额 + 剩余可用信用
```

## 影响范围

### 修改的文件
1. `src/FeeQuery.Providers/MiniMax/MiniMaxProvider.cs` - 余额计算逻辑
2. `src/FeeQuery.Shared/Models/AccountBalance.cs` - 模型扩展
3. `src/FeeQuery.Web/Components/Pages/BalanceDashboard.razor` - 界面显示

### 兼容性
- ✅ 向后兼容：其他云厂商不受影响
- ✅ 数据库无变更：无需执行迁移
- ✅ 现有数据正常：历史余额记录不受影响

## 后续建议

1. **余额预警优化**：当检测到透支时，自动触发高优先级预警
2. **账单功能**：添加 MiniMax 账单查询功能，显示消费明细
3. **充值提醒**：在透支超过一定阈值时，发送紧急通知
4. **其他厂商**：检查阿里云、腾讯云等其他支持信用额度的厂商是否有类似问题

## 修改时间

2026-01-12
