using FeeQuery.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FeeQuery.Tests;

/// <summary>
/// CredentialEncryptionService 单元测试
/// </summary>
public class CredentialEncryptionServiceTests
{
    private static CredentialEncryptionService CreateDataProtectionService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Security:EncryptionProvider", "DataProtection" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<IDataProtectionProvider>();
        return new CredentialEncryptionService(config, provider);
    }

    private static CredentialEncryptionService CreateAesService(string? keyBase64 = null)
    {
        var dict = new Dictionary<string, string?> { { "Security:EncryptionProvider", "Aes" } };
        if (keyBase64 != null)
            dict["Security:AesKey"] = keyBase64;

        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        return new CredentialEncryptionService(config);
    }

    // ────────────── DataProtection 模式 ──────────────

    [Fact]
    public void DataProtection_加密后应无法直接读取原始值()
    {
        var service = CreateDataProtectionService();
        var credentials = new Dictionary<string, string>
        {
            { "AccessKeyId", "my-access-key" },
            { "SecretAccessKey", "my-secret-key" }
        };

        var encrypted = service.Encrypt(credentials);

        Assert.StartsWith("dp1:", encrypted);
        Assert.DoesNotContain("my-access-key", encrypted);
        Assert.DoesNotContain("my-secret-key", encrypted);
    }

    [Fact]
    public void DataProtection_加密后解密应还原原始数据()
    {
        var service = CreateDataProtectionService();
        var credentials = new Dictionary<string, string>
        {
            { "AccessKeyId", "ak-12345" },
            { "SecretAccessKey", "sk-abcde" }
        };

        var encrypted = service.Encrypt(credentials);
        var decrypted = service.Decrypt(encrypted);

        Assert.Equal("ak-12345", decrypted["AccessKeyId"]);
        Assert.Equal("sk-abcde", decrypted["SecretAccessKey"]);
    }

    // ────────────── AES 模式 ──────────────

    [Fact]
    public void Aes_加密后应无法直接读取原始值()
    {
        var key = Convert.ToBase64String(new byte[32]);
        var service = CreateAesService(key);
        var credentials = new Dictionary<string, string> { { "ApiKey", "secret-api-key" } };

        var encrypted = service.Encrypt(credentials);

        Assert.StartsWith("aes1:", encrypted);
        Assert.DoesNotContain("secret-api-key", encrypted);
    }

    [Fact]
    public void Aes_加密后解密应还原原始数据()
    {
        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var service = CreateAesService(key);
        var credentials = new Dictionary<string, string>
        {
            { "AccessKeyId", "ak-aes-test" },
            { "SecretAccessKey", "sk-aes-test" },
            { "Region", "cn-north-1" }
        };

        var encrypted = service.Encrypt(credentials);
        var decrypted = service.Decrypt(encrypted);

        Assert.Equal(3, decrypted.Count);
        Assert.Equal("ak-aes-test", decrypted["AccessKeyId"]);
        Assert.Equal("sk-aes-test", decrypted["SecretAccessKey"]);
        Assert.Equal("cn-north-1", decrypted["Region"]);
    }

    [Fact]
    public void Aes_相同数据每次加密结果应不同但均可解密()
    {
        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var service = CreateAesService(key);
        var credentials = new Dictionary<string, string> { { "Key", "value" } };

        var enc1 = service.Encrypt(credentials);
        var enc2 = service.Encrypt(credentials);

        Assert.NotEqual(enc1, enc2); // 每次随机 IV，密文不同
        Assert.Equal("value", service.Decrypt(enc1)["Key"]);
        Assert.Equal("value", service.Decrypt(enc2)["Key"]);
    }

    // ────────────── 通用兼容性 ──────────────

    [Fact]
    public void 解密旧版明文JSON应降级兼容并返回正确数据()
    {
        var service = CreateDataProtectionService();
        var legacyJson = """{"AccessKeyId":"old-key","SecretAccessKey":"old-secret"}""";

        var result = service.Decrypt(legacyJson);

        Assert.Equal("old-key", result["AccessKeyId"]);
        Assert.Equal("old-secret", result["SecretAccessKey"]);
    }

    [Fact]
    public void 解密空字符串应返回空字典()
    {
        var service = CreateDataProtectionService();
        Assert.Empty(service.Decrypt(string.Empty));
    }

    [Fact]
    public void 解密null应返回空字典()
    {
        var service = CreateDataProtectionService();
        Assert.Empty(service.Decrypt(null!));
    }

    [Fact]
    public void 加密空字典应可正常往返()
    {
        var service = CreateDataProtectionService();
        var encrypted = service.Encrypt(new Dictionary<string, string>());
        Assert.Empty(service.Decrypt(encrypted));
    }
}
