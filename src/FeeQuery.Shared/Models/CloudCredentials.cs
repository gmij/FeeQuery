namespace FeeQuery.Shared.Models;

/// <summary>
/// 云厂商凭证（用于传递解密后的凭证数据）
/// </summary>
public class CloudCredentials
{
    /// <summary>
    /// 凭证字典（键值对形式存储各种凭证信息）
    /// 例如：{ "AccessKeyId": "xxx", "AccessKeySecret": "xxx" }
    /// </summary>
    public Dictionary<string, string> Credentials { get; set; } = new();

    /// <summary>
    /// 获取凭证值
    /// </summary>
    public string? GetCredential(string key)
    {
        return Credentials.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// 设置凭证值
    /// </summary>
    public void SetCredential(string key, string value)
    {
        Credentials[key] = value;
    }
}
