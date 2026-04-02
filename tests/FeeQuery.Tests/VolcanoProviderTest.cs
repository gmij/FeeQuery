using FeeQuery.Providers.Volcano;
using FeeQuery.Shared.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FeeQuery.Tests;

/// <summary>
/// 火山云适配器测试
/// </summary>
public class VolcanoProviderTest
{
    private readonly ITestOutputHelper _output;
    private readonly VolcanoCloudProvider _provider;

    public VolcanoProviderTest(ITestOutputHelper output)
    {
        _output = output;

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        var logger = loggerFactory.CreateLogger<VolcanoCloudProvider>();

        var httpClientFactory = new TestHttpClientFactory();
        _provider = new VolcanoCloudProvider(httpClientFactory, logger);
    }

    [Fact]
    public async Task Test_ValidateCredentials()
    {
        var accessKeyId = Environment.GetEnvironmentVariable("VOLCANO_ACCESS_KEY_ID");
        var secretAccessKey = Environment.GetEnvironmentVariable("VOLCANO_SECRET_ACCESS_KEY");
        if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey))
        {
            _output.WriteLine("跳过测试：未设置火山云凭证环境变量 VOLCANO_ACCESS_KEY_ID / VOLCANO_SECRET_ACCESS_KEY");
            return;
        }

        _output.WriteLine("开始测试火山云凭证验证...");

        var credentials = new CloudCredentials();
        credentials.SetCredential("AccessKeyId", accessKeyId);
        credentials.SetCredential("SecretAccessKey", secretAccessKey);

        var result = await _provider.ValidateCredentialsAsync(credentials);
        _output.WriteLine($"验证结果: {result}");
        Assert.True(result, "凭证验证应该成功");
    }

    [Fact]
    public async Task Test_GetAccountBalance()
    {
        var accessKeyId = Environment.GetEnvironmentVariable("VOLCANO_ACCESS_KEY_ID");
        var secretAccessKey = Environment.GetEnvironmentVariable("VOLCANO_SECRET_ACCESS_KEY");
        if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey))
        {
            _output.WriteLine("跳过测试：未设置火山云凭证环境变量 VOLCANO_ACCESS_KEY_ID / VOLCANO_SECRET_ACCESS_KEY");
            return;
        }

        _output.WriteLine("开始测试火山云余额查询...");

        var credentials = new CloudCredentials();
        credentials.SetCredential("AccessKeyId", accessKeyId);
        credentials.SetCredential("SecretAccessKey", secretAccessKey);

        var balance = await _provider.GetAccountBalanceAsync(credentials);
        _output.WriteLine($"可用余额: {balance.AvailableBalance} {balance.Currency}");
        _output.WriteLine($"信用额度: {balance.CreditLimit} {balance.Currency}");
        _output.WriteLine($"查询时间: {balance.QueryTime}");
        Assert.NotNull(balance);
    }

    /// <summary>
    /// 测试用HttpClientFactory
    /// </summary>
    private class TestHttpClientFactory : IHttpClientFactory
    {
        public System.Net.Http.HttpClient CreateClient(string name)
        {
            return new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }
    }
}
