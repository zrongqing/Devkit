using System.Net;
using System.Net.Http;
using Barcode2.Configuration;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class Barcode2ConnectionTesterTests
{
    [Fact]
    public async Task Environment_test_checks_server_and_database_with_current_credentials()
    {
        var environment = CreateEnvironment();
        environment.DatabaseUsername = "current-user";
        environment.DatabasePassword = "current-password";
        Barcode2EnvironmentSettings? tested = null;
        var tester = new Barcode2ConnectionTester(
            new HttpClient(new ResponseHandler(HttpStatusCode.OK)),
            (settings, _) =>
            {
                tested = settings;
                return Task.CompletedTask;
            },
            (_, _) => Task.CompletedTask);

        await tester.TestEnvironmentAsync(environment, TestContext.Current.CancellationToken);

        var actual = Assert.IsType<Barcode2EnvironmentSettings>(tested);
        Assert.Same(environment, actual);
        Assert.Equal("current-user", actual.DatabaseUsername);
        Assert.Equal("current-password", actual.DatabasePassword);
    }

    [Fact]
    public async Task Server_failure_returns_sanitized_error()
    {
        var environment = CreateEnvironment();
        environment.DatabaseUsername = "secret-user";
        environment.DatabasePassword = "secret-password";
        var tester = new Barcode2ConnectionTester(
            new HttpClient(new ThrowingHandler()),
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tester.TestEnvironmentAsync(environment, TestContext.Current.CancellationToken));

        Assert.Contains("服务器地址", exception.Message);
        Assert.DoesNotContain("secret-user", exception.Message);
        Assert.DoesNotContain("secret-password", exception.Message);
    }

    [Fact]
    public async Task Missing_environment_credentials_are_rejected_before_access()
    {
        var environment = CreateEnvironment();
        environment.DatabasePassword = string.Empty;
        var tester = new Barcode2ConnectionTester(
            new HttpClient(new ResponseHandler(HttpStatusCode.OK)),
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask);

        var exception = await Assert.ThrowsAsync<Barcode2ConfigurationException>(() =>
            tester.TestEnvironmentAsync(environment, TestContext.Current.CancellationToken));

        Assert.Contains("不完整", exception.Message);
    }

    [Fact]
    public async Task Share_test_uses_the_selected_targets_independent_credentials()
    {
        var target = Barcode2Defaults.Create().ShareTargets[2];
        target.Root = @"\\fileserver.example.invalid\webapp";
        target.Username = "target-user";
        target.Password = "target-password";
        Barcode2ShareSettings? tested = null;
        var tester = new Barcode2ConnectionTester(
            new HttpClient(new ResponseHandler(HttpStatusCode.OK)),
            (_, _) => Task.CompletedTask,
            (settings, _) =>
            {
                tested = settings;
                return Task.CompletedTask;
            });

        await tester.TestShareAsync(target, TestContext.Current.CancellationToken);

        var actual = Assert.IsType<Barcode2ShareSettings>(tested);
        Assert.Same(target, actual);
        Assert.Equal("target-user", actual.Username);
        Assert.Equal("target-password", actual.Password);
    }

    [Fact]
    public async Task Missing_share_path_is_rejected_before_access()
    {
        var target = Barcode2Defaults.Create().ShareTargets[0];
        var tester = new Barcode2ConnectionTester(
            new HttpClient(new ResponseHandler(HttpStatusCode.OK)),
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask);

        var exception = await Assert.ThrowsAsync<Barcode2ConfigurationException>(() =>
            tester.TestShareAsync(target, TestContext.Current.CancellationToken));

        Assert.Contains("UNC", exception.Message);
    }

    private static Barcode2EnvironmentSettings CreateEnvironment()
    {
        var environment = Barcode2Defaults.Create().Environments[0];
        environment.DatabaseDataSource = "db.example.invalid/service";
        environment.DatabaseUsername = "user";
        environment.DatabasePassword = "password";
        environment.PageBaseAddress = "https://app.example.invalid";
        return environment;
    }

    private sealed class ResponseHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("network unavailable");
        }
    }
}
