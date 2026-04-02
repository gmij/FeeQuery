using FeeQuery.Shared.Models;

namespace FeeQuery.Shared.Interfaces;

/// <summary>
/// 云厂商统一接口
/// 所有云厂商适配器必须实现此接口
/// </summary>
public interface ICloudProvider
{
    /// <summary>
    /// 厂商唯一标识符（如 "alibaba", "tencent"）
    /// 用于标识和路由到具体的厂商实现
    /// </summary>
    string ProviderCode { get; }

    /// <summary>
    /// 厂商显示名称（如 "阿里云", "腾讯云"）
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 厂商描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 验证账号凭证是否有效
    /// </summary>
    /// <param name="credentials">解密后的凭证信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>验证是否成功</returns>
    Task<bool> ValidateCredentialsAsync(CloudCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取账户余额信息
    /// </summary>
    /// <param name="credentials">解密后的凭证信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>账户余额</returns>
    Task<AccountBalance> GetAccountBalanceAsync(
        CloudCredentials credentials,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取此厂商所需的凭证字段列表
    /// 用于前端动态生成凭证输入表单
    /// </summary>
    /// <returns>凭证字段定义列表</returns>
    List<CredentialField> GetRequiredCredentialFields();
}
