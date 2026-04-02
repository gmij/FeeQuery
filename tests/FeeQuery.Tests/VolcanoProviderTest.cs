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

        // 创建日志记录器
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        var logger = loggerFactory.CreateLogger<VolcanoCloudProvider>();

        // 创建HttpClientFactory
        var httpClientFactory = new TestHttpClientFactory();

        _provider = new VolcanoCloudProvider(httpClientFactory, logger);
    }

    [Fact]
    public async Task Test_ValidateCredentials()
    {
        _output.WriteLine("开始测试火山云凭证验证...");

        // 从数据库读取真实凭证进行测试
        var credentials = new CloudCredentials();
        // 使用环境变量或用户机密提供真实凭证进行集成测试
        credentials.SetCredential("AccessKeyId", Environment.GetEnvironmentVariable("VOLCANO_ACCESS_KEY_ID") ?? "YOUR_ACCESS_KEY_ID");
        credentials.SetCredential("SecretAccessKey", Environment.GetEnvironmentVariable("VOLCANO_SECRET_ACCESS_KEY") ?? "YOUR_SECRET_ACCESS_KEY");

        try
        {
            _output.WriteLine($"AccessKeyId: {credentials.GetCredential("AccessKeyId")}");
            _output.WriteLine($"SecretAccessKey: {credentials.GetCredential("SecretAccessKey")?[..10]}...");

            var result = await _provider.ValidateCredentialsAsync(credentials);

            _output.WriteLine($"验证结果: {result}");
            Assert.True(result, "凭证验证应该成功");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"测试失败: {ex.Message}");
            _output.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            throw;
        }
    }

    [Fact]
    public async Task Test_GetAccountBalance()
    {
        _output.WriteLine("开始测试火山云余额查询...");

        var credentials = new CloudCredentials();
        credentials.SetCredential("AccessKeyId", Environment.GetEnvironmentVariable("VOLCANO_ACCESS_KEY_ID") ?? "YOUR_ACCESS_KEY_ID");
        credentials.SetCredential("SecretAccessKey", Environment.GetEnvironmentVariable("VOLCANO_SECRET_ACCESS_KEY") ?? "YOUR_SECRET_ACCESS_KEY");

        try
        {
            var balance = await _provider.GetAccountBalanceAsync(credentials);

            _output.WriteLine($"可用余额: {balance.AvailableBalance} {balance.Currency}");
            _output.WriteLine($"信用额度: {balance.CreditLimit} {balance.Currency}");
            _output.WriteLine($"查询时间: {balance.QueryTime}");

            Assert.NotNull(balance);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"测试失败: {ex.Message}");
            _output.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// 测试用HttpClientFactory
    /// </summary>
    private class TestHttpClientFactory : IHttpClientFactory
    {
        public System.Net.Http.HttpClient CreateClient(string name)
        {
            var handler = new System.Net.Http.HttpClientHandler
            {
                // 设置较短的超时时间用于测试
                // 如果需要使用代理，可以在这里配置
            };

            var client = new System.Net.Http.HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30) // 30秒超时
            };

            return client;
        }
    }
}
