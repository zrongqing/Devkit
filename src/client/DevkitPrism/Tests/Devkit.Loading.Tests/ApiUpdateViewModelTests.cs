using System.IO;
using Ssamc.Core.ApiCodeCollector;
using Ssamc.Servers;
using Devkit.Services.Interfaces;
using Ssamc.ViewModels;
using Moq;
using Xunit;

namespace Devkit.Loading.Tests;

[Collection("Ssamc environment")]
public sealed class ApiUpdateViewModelTests : IDisposable
{
    private const string FirstConnectionVariable = "DEVKIT_SSAMC_DB_215_58_CONNECTION";
    private const string SecondConnectionVariable = "DEVKIT_SSAMC_DB_20_54_CONNECTION";
    private readonly string? _firstOriginal = Environment.GetEnvironmentVariable(FirstConnectionVariable);
    private readonly string? _secondOriginal = Environment.GetEnvironmentVariable(SecondConnectionVariable);
    private readonly string _sourceDirectory;

    public ApiUpdateViewModelTests()
    {
        _sourceDirectory = Path.Combine(Path.GetTempPath(), $"devkit-loading-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sourceDirectory);
    }

    [Fact]
    public async Task All_four_buttons_use_page_loading_state()
    {
        Environment.SetEnvironmentVariable(FirstConnectionVariable, "first-connection");
        Environment.SetEnvironmentVariable(SecondConnectionVariable, "second-connection");
        var server = CreateSuccessfulServer();
        var viewModel = CreateViewModel(server.Object);
        var busyStarts = 0;
        viewModel.PageLoading.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.PageLoading.IsBusy) && viewModel.PageLoading.IsBusy)
            {
                busyStarts++;
            }
        };

        await viewModel.ScanSourceCodeCommand.ExecuteAsync(null);
        viewModel.StrSelectApiCodes = "API001";
        await viewModel.PreviewCommand.ExecuteAsync(null);
        viewModel.StrUpdateApis = "API001";
        await viewModel.UpdateCommand.ExecuteAsync("215.58");
        await viewModel.UpdateCommand.ExecuteAsync("20.54");

        Assert.Equal(4, busyStarts);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
        Assert.Equal("execution", viewModel.SourceCodePreview);
        Assert.Equal("extension", viewModel.CodePreview);
        server.Verify(
            service => service.UpdateExtendCode("API001", "extension", It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Missing_target_configuration_reports_error_and_resets_loading()
    {
        Environment.SetEnvironmentVariable(FirstConnectionVariable, null);
        var server = CreateSuccessfulServer();
        var viewModel = CreateViewModel(server.Object);
        viewModel.StrUpdateApis = "API001";

        await viewModel.UpdateCommand.ExecuteAsync("215.58");

        Assert.Contains(FirstConnectionVariable, viewModel.LastNotificationMessage);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
        server.Verify(
            service => service.UpdateExtendCode(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Server_failure_reports_error_and_resets_loading()
    {
        Environment.SetEnvironmentVariable(FirstConnectionVariable, "connection");
        var server = CreateSuccessfulServer();
        server.Setup(service => service.UpdateExtendCode(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Throws(new InvalidOperationException("database unavailable"));
        var viewModel = CreateViewModel(server.Object);
        viewModel.StrUpdateApis = "API001";

        await viewModel.UpdateCommand.ExecuteAsync("215.58");

        Assert.Contains("database unavailable", viewModel.LastNotificationMessage);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
    }

    [Fact]
    public async Task A_second_page_command_is_ignored_while_one_is_running()
    {
        var started = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var server = CreateSuccessfulServer();
        server.Setup(service => service.GetAllApiSourceInfos(_sourceDirectory))
            .Returns(() =>
            {
                started.Set();
                release.Wait(TimeSpan.FromSeconds(2));
                return [new ApiSourceInfo { ApiCodes = ["API001"] }];
            });
        var viewModel = CreateViewModel(server.Object);
        viewModel.StrSelectApiCodes = "API001";

        var scan = viewModel.ScanSourceCodeCommand.ExecuteAsync(null);
        Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
        await viewModel.PreviewCommand.ExecuteAsync(null);
        release.Set();
        await scan;

        server.Verify(
            service => service.GetExecutionSourceCodeByApiCode(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
        Assert.False(viewModel.PageLoading.IsBusy);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(FirstConnectionVariable, _firstOriginal);
        Environment.SetEnvironmentVariable(SecondConnectionVariable, _secondOriginal);
        Directory.Delete(_sourceDirectory, recursive: true);
    }

    private ApiUpdateViewModel CreateViewModel(IApiUpdateServer server)
    {
        var files = new Mock<IFileService>();
        var storage = new Mock<IModuleStorage>();
        storage.Setup(service => service.GetModulePath("ssamc")).Returns(_sourceDirectory);

        return new ApiUpdateViewModel(server, files.Object, storage.Object)
        {
            SourceCodePath = _sourceDirectory
        };
    }

    private Mock<IApiUpdateServer> CreateSuccessfulServer()
    {
        var server = new Mock<IApiUpdateServer>();
        var sourceInfo = new ApiSourceInfo { ApiCodes = ["API001"] };
        server.Setup(service => service.GetAllApiSourceInfos(_sourceDirectory)).Returns([sourceInfo]);
        server.Setup(service => service.GetAllApiCodes(It.IsAny<List<ApiSourceInfo>>()))
            .Returns(["API001"]);
        server.Setup(service => service.GetExecutionSourceCodeByApiCode(_sourceDirectory, "API001"))
            .Returns("execution");
        server.Setup(service => service.GetSourceCodeByApiCode(_sourceDirectory, "API001"))
            .Returns("extension");
        server.Setup(service => service.GetSourceCodeByApiCode(It.IsAny<List<ApiSourceInfo>>(), "API001"))
            .Returns("extension");
        server.Setup(service => service.UpdateExtendCode(
                "API001",
                "extension",
                It.IsAny<string>()))
            .Returns(true);
        return server;
    }
}

[CollectionDefinition("Ssamc environment", DisableParallelization = true)]
public sealed class SsamcEnvironmentCollection
{
}
