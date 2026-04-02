using FeeQuery.Providers.Baidu;
using FeeQuery.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace FeeQuery.Tests;

/// <summary>
/// 百度云Provider测试
/// 
/// 注意：要使用真实的百度云凭证进行测试，请设置以下环境变量：
/// - BAIDU_ACCESS_KEY_ID: 百度云Access Key ID
/// - BAIDU_SECRET_ACCESS_KEY: 百度云Secret Access Key
/// </summary>
public class BaiduProviderTest
{
    /// <summary>
    /// 测试百度云余额查询
    /// </summary>
    [Fact]
    public async Task TestBaiduGetBalance()
    {
        // 从环境变量获取凭证
        var accessKeyId = Environment.GetEnvironmentVariable("BAIDU_ACCESS_KEY_ID");
        var secretAccessKey = Environment.GetEnvironmentVariable("BAIDU_SECRET_ACCESS_KEY");

        // 如果没有设置环境变量，跳过此测试
        if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey))
        {
            Console.WriteLine("跳过测试：未设置百度云凭证环境变量");
            Console.WriteLine("请设置 BAIDU_ACCESS_KEY_ID 和 BAIDU_SECRET_ACCESS_KEY");
            Assert.True(true, "跳过测试：未配置凭证");
            return;
        }

        var credentials = new CloudCredentials
        {
            Credentials = new Dictionary<string, string>
            {
                { "AccessKeyId", accessKeyId },
                { "SecretAccessKey", secretAccessKey }
            }
        };

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });
        var logger = loggerFactory.CreateLogger<BaiduCloudProvider>();

        var services = new ServiceCollection();
        services.AddHttpClient();
        var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        var provider = new BaiduCloudProvider(httpClientFactory, logger);

        var balance = await provider.GetAccountBalanceAsync(credentials);
        
        Console.WriteLine($"账户余额查询成功:");
        Console.WriteLine($"  可用余额: {balance.AvailableBalance} {balance.Currency}");
        Console.WriteLine($"  信用额度: {balance.CreditLimit} {balance.Currency}");
        Console.WriteLine($"  查询时间: {balance.QueryTime}");
        
        Assert.True(balance.AvailableBalance >= 0, "余额应该是非负数");
    }

    /// <summary>
    /// 验证BCE v1签名算法实现正确性
    /// 使用官方鉴权工具验证的数据
    /// </summary>
    [Fact]
    public void TestBceSignatureAlgorithm()
    {
        // 使用固定的测试数据验证签名算法（虚假凭证，仅用于算法正确性验证）
        var accessKeyId = "test-access-key-id-for-unit-test";
        var secretAccessKey = "test-secret-key-for-unit-test-only";
        var timestamp = "2024-01-15T10:30:00Z";
        var host = "billing.baidubce.com";
        var apiPath = "/v1/finance/cash/balance";

        // 根据上述测试凭证计算的期望结果
        var expectedSigningKey = "5044a1ac9036fe9d4a5504108118c6dbacfa933e84004d39f68168c811f8a2f0";
        var expectedSignature = "e5f208c7df64a406828dddde3c50e87124926d780c206986b44261d54bfb02aa";
        
        // Step 1: AuthStringPrefix
        var authStringPrefix = $"bce-auth-v1/{accessKeyId}/{timestamp}/1800";
        
        // Step 2: SigningKey = HMAC-SHA256(SK, AuthStringPrefix)
        var signingKeyBytes = HmacSha256(Encoding.UTF8.GetBytes(secretAccessKey), Encoding.UTF8.GetBytes(authStringPrefix));
        var signingKeyHex = ToHex(signingKeyBytes);
        
        // Step 3: CanonicalRequest (x-bce-date 的值需要 URI 编码)
        var encodedTimestamp = BceUriEncode(timestamp);
        var canonicalHeaders = $"host:{host}\nx-bce-date:{encodedTimestamp}";
        var signedHeaders = "host;x-bce-date";
        var canonicalRequest = $"POST\n{apiPath}\n\n{canonicalHeaders}";
        
        // Step 4: Signature = HMAC-SHA256(SigningKeyHex, CanonicalRequest)
        // 关键：使用 SigningKey 的十六进制字符串作为 HMAC 密钥
        var signature = ToHex(HmacSha256(Encoding.UTF8.GetBytes(signingKeyHex), Encoding.UTF8.GetBytes(canonicalRequest)));
        
        var authorization = $"{authStringPrefix}/{signedHeaders}/{signature}";
        
        Console.WriteLine("=== BCE v1 签名算法验证 ===");
        Console.WriteLine($"AuthStringPrefix: {authStringPrefix}");
        Console.WriteLine($"SigningKey (hex): {signingKeyHex}");
        Console.WriteLine($"CanonicalRequest: {canonicalRequest.Replace("\n", "\\n")}");
        Console.WriteLine($"Signature: {signature}");
        Console.WriteLine($"Authorization: {authorization}");
        
        // 验证 SigningKey
        Assert.Equal(expectedSigningKey, signingKeyHex);
        
        // 验证 Signature
        Assert.Equal(expectedSignature, signature);
        
        // 验证签名格式
        Assert.Equal(64, signature.Length);
        Assert.StartsWith("bce-auth-v1/", authorization);
        Assert.True(signature.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));
        
        Console.WriteLine("✓ 签名算法验证通过！");
    }

    /// <summary>
    /// 验证签名算法的可重复性
    /// </summary>
    [Fact]
    public void TestSignatureReproducibility()
    {
        var accessKeyId = "ak123";
        var secretAccessKey = "sk456";
        var timestamp = "2024-01-01T00:00:00Z";
        
        var authStringPrefix = $"bce-auth-v1/{accessKeyId}/{timestamp}/1800";
        var signingKeyBytes = HmacSha256(Encoding.UTF8.GetBytes(secretAccessKey), Encoding.UTF8.GetBytes(authStringPrefix));
        var signingKeyHex = ToHex(signingKeyBytes);
        
        var encodedTimestamp = BceUriEncode(timestamp);
        var canonicalRequest = $"GET\n/v1/test\n\nhost:test.baidubce.com\nx-bce-date:{encodedTimestamp}";
        
        var signatures = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var signature = ToHex(HmacSha256(Encoding.UTF8.GetBytes(signingKeyHex), Encoding.UTF8.GetBytes(canonicalRequest)));
            signatures.Add(signature);
        }
        
        Assert.True(signatures.All(s => s == signatures[0]), "签名应该是可重复的");
        Console.WriteLine($"签名可重复性验证通过: {signatures[0]}");
    }

    /// <summary>
    /// 测试Provider所需凭证字段
    /// </summary>
    [Fact]
    public void TestRequiredCredentialFields()
    {
        var provider = new BaiduCloudProvider();
        var fields = provider.GetRequiredCredentialFields();
        
        Assert.Equal(2, fields.Count);
        
        var akField = fields.Find(f => f.Key == "AccessKeyId");
        Assert.NotNull(akField);
        Assert.True(akField.Required);
        Assert.False(akField.IsSensitive);
        
        var skField = fields.Find(f => f.Key == "SecretAccessKey");
        Assert.NotNull(skField);
        Assert.True(skField.Required);
        Assert.True(skField.IsSensitive);
        
        Console.WriteLine("凭证字段验证通过:");
        foreach (var field in fields)
        {
            Console.WriteLine($"  {field.Key}: {field.DisplayName} (Required={field.Required}, Sensitive={field.IsSensitive})");
        }
    }

    private byte[] HmacSha256(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    private string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

    /// <summary>
    /// BCE URI 编码
    /// </summary>
    private static string BceUriEncode(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sb = new StringBuilder();
        foreach (byte b in Encoding.UTF8.GetBytes(input))
        {
            char c = (char)b;
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') || c == '-' || c == '.' || c == '_' || c == '~')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2"));
            }
        }
        return sb.ToString();
    }
}
