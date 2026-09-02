using System.IO;
using System.Net.Http;
using Devkit.Core.Helpers;
using Microsoft.EntityFrameworkCore;
using Barcode2.DB.Context;

namespace Barcode2.Configuration;

public sealed class Barcode2ConnectionTester : IBarcode2ConnectionTester
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private readonly HttpClient _httpClient;
    private readonly Func<Barcode2EnvironmentSettings, CancellationToken, Task> _databaseTester;
    private readonly Func<Barcode2ShareSettings, CancellationToken, Task> _shareTester;

    public Barcode2ConnectionTester(HttpClient httpClient)
        : this(httpClient, TestDatabaseCoreAsync, TestShareCoreAsync)
    {
    }

    internal Barcode2ConnectionTester(
        HttpClient httpClient,
        Func<Barcode2EnvironmentSettings, CancellationToken, Task> databaseTester,
        Func<Barcode2ShareSettings, CancellationToken, Task> shareTester)
    {
        _httpClient = httpClient;
        _databaseTester = databaseTester;
        _shareTester = shareTester;
    }

    public async Task TestEnvironmentAsync(
        Barcode2EnvironmentSettings environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _ = Barcode2ConfigurationService.BuildConnectionString(environment);
        Barcode2ConfigurationService.ValidatePageAddress(environment);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            try
            {
                await TestServerAsync(environment.PageBaseAddress, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"{environment.DisplayName}服务器地址无法正常访问。",
                    exception);
            }

            try
            {
                await _databaseTester(environment, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"{environment.DisplayName} Oracle 数据库连接失败，请检查数据源及账号密码。",
                    exception);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{environment.DisplayName}连接检测超时。");
        }
    }

    public async Task TestShareAsync(
        Barcode2ShareSettings target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ValidateShare(target);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            await _shareTester(target, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{target.DisplayName}连接检测超时。");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Barcode2ConfigurationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"{target.DisplayName}连接失败，请检查发布目录及账号密码。",
                exception);
        }
    }

    private async Task TestServerAsync(string baseAddress, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, baseAddress);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task TestDatabaseCoreAsync(
        Barcode2EnvironmentSettings environment,
        CancellationToken cancellationToken)
    {
        var connectionString = Barcode2ConfigurationService.BuildConnectionString(environment);
        await using var context = new MyDbContext(connectionString);
        if (!await context.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException("Oracle 数据库拒绝连接。");
        }
    }

    private static Task TestShareCoreAsync(
        Barcode2ShareSettings target,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            using var uploader = new NetworkShareUploader(
                target.Root,
                target.Username,
                target.Password);
            uploader.Connect();
            if (!Directory.Exists(target.Root))
            {
                throw new DirectoryNotFoundException("连接后仍无法访问共享目录。");
            }
        }, cancellationToken);
    }

    private static void ValidateShare(Barcode2ShareSettings target)
    {
        if (string.IsNullOrWhiteSpace(target.Root) ||
            !target.Root.Trim().StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new Barcode2ConfigurationException(
                $"{target.DisplayName}必须配置有效的 UNC 共享路径。");
        }

    }
}
