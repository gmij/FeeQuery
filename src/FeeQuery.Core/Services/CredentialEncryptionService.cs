using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FeeQuery.Core.Services;

/// <summary>
/// 凭证加密服务，支持两种加密策略，通过 Security:EncryptionProvider 配置项选择：
///
/// - DataProtection（默认）：使用 .NET Data Protection API，密钥持久化到数据库或文件。
///   优点：标准、安全；缺点：密钥和数据库必须同时备份。
///
/// - Aes：AES-256-CBC 对称加密，密钥由配置项 Security:AesKey 提供。
///   优点：密钥只是一段字符串，易于备份和迁移；缺点：需要自行保管密钥。
///
/// 两种格式可共存，Decrypt 方法通过前缀自动识别格式：
///   "aes1:" 前缀 → AES 解密
///   "dp1:"  前缀 → DataProtection 解密
///   "{" 开头     → 旧版明文 JSON（降级兼容，提示重新保存）
///   其他         → 尝试无前缀的旧 DataProtection 格式（兼容历史数据）
/// </summary>
public class CredentialEncryptionService
{
    private readonly IDataProtector? _protector;
    private readonly byte[]? _aesKey;
    private readonly ILogger<CredentialEncryptionService>? _logger;
    private readonly string _mode;

    private const string AesPrefix = "aes1:";
    private const string DpPrefix = "dp1:";

    public CredentialEncryptionService(
        IConfiguration configuration,
        IDataProtectionProvider? protectionProvider = null,
        ILogger<CredentialEncryptionService>? logger = null)
    {
        _logger = logger;
        _mode = configuration.GetValue<string>("Security:EncryptionProvider") ?? "DataProtection";

        if (_mode.Equals("Aes", StringComparison.OrdinalIgnoreCase))
        {
            var keyBase64 = configuration.GetValue<string>("Security:AesKey");
            if (string.IsNullOrEmpty(keyBase64))
            {
                // 未配置密钥时自��生成临时密钥，重启后失效，需用户保存
                _aesKey = RandomNumberGenerator.GetBytes(32);
                _logger?.LogWarning(
                    "Security:AesKey 未配置，已生成临时随机密钥（重启后失效，已存凭证将无法解密）。" +
                    "请将以下密钥添加到配置文件或环境变量后重启：Security__AesKey={Key}",
                    Convert.ToBase64String(_aesKey));
            }
            else
            {
                _aesKey = Convert.FromBase64String(keyBase64);
            }
        }
        else
        {
            if (protectionProvider == null)
                throw new InvalidOperationException("DataProtection 模式下必须注册 IDataProtectionProvider");
            _protector = protectionProvider.CreateProtector("FeeQuery.CloudCredentials.v1");
        }
    }

    /// <summary>
    /// 加密凭证字典。
    /// DataProtection 模式输出 "dp1:" 前缀；AES 模式输出 "aes1:" 前缀。
    /// </summary>
    public string Encrypt(Dictionary<string, string> credentials)
    {
        var json = JsonSerializer.Serialize(credentials);
        if (_mode.Equals("Aes", StringComparison.OrdinalIgnoreCase))
            return AesPrefix + AesEncrypt(json);
        return DpPrefix + _protector!.Protect(json);
    }

    /// <summary>
    /// 解密凭证字典，自动识别加密格式（兼容所有历史格式）。
    /// </summary>
    public Dictionary<string, string> Decrypt(string encryptedCredentials)
    {
        try
        {
            if (string.IsNullOrEmpty(encryptedCredentials))
                return new Dictionary<string, string>();

            string json;

            if (encryptedCredentials.StartsWith(AesPrefix))
            {
                // AES 格式
                if (_aesKey == null)
                {
                    _logger?.LogWarning("凭证使用 AES 格式加密，但当前配置为 DataProtection 模式，无法解密，请切换 Security:EncryptionProvider=Aes 或重新输入凭证");
                    return new Dictionary<string, string>();
                }
                json = AesDecrypt(encryptedCredentials[AesPrefix.Length..]);
            }
            else if (encryptedCredentials.StartsWith(DpPrefix))
            {
                // DataProtection 格式（带前缀）
                if (_protector == null)
                {
                    _logger?.LogWarning("凭证使用 DataProtection 格式加密，但当前配置为 AES 模式，无法解密，请切换 Security:EncryptionProvider=DataProtection 或重新输入凭证");
                    return new Dictionary<string, string>();
                }
                json = _protector.Unprotect(encryptedCredentials[DpPrefix.Length..]);
            }
            else if (encryptedCredentials.TrimStart().StartsWith('{'))
            {
                // 旧版明文 JSON（历史数据降级兼容）
                _logger?.LogWarning("检测到账号凭证使用旧版明文格式，请通过界面重新保存该账号以完成加密升级");
                json = encryptedCredentials;
            }
            else
            {
                // 无前缀的旧 DataProtection 格式（历史数据降级兼容）
                if (_protector == null)
                {
                    _logger?.LogWarning("无法识别凭证格式（当前为 AES 模式），请重新输入账号凭证");
                    return new Dictionary<string, string>();
                }
                try
                {
                    json = _protector.Unprotect(encryptedCredentials);
                }
                catch (CryptographicException)
                {
                    _logger?.LogWarning("凭证解密失败（可能是旧版 DataProtection 格式且密钥已更换），请通过界面重新输入账号凭证");
                    return new Dictionary<string, string>();
                }
            }

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "解析凭证失败，字符串长度: {Length}", encryptedCredentials?.Length ?? 0);
            return new Dictionary<string, string>();
        }
    }

    private string AesEncrypt(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _aesKey!;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
        // 输出格式：Base64(IV[16字节] + 密文)
        var result = new byte[aes.IV.Length + ciphertext.Length];
        aes.IV.CopyTo(result, 0);
        ciphertext.CopyTo(result, aes.IV.Length);
        return Convert.ToBase64String(result);
    }

    private string AesDecrypt(string base64Ciphertext)
    {
        var data = Convert.FromBase64String(base64Ciphertext);
        using var aes = Aes.Create();
        aes.Key = _aesKey!;
        aes.IV = data[..16];
        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(data, 16, data.Length - 16);
        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
