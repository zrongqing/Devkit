using System.IO;
using Barcode2.Core.ApiCodeCollector;
using Barcode2.Servers;
using Devkit.Services.Interfaces.Notifications;
using Barcode2.Configuration;
using Barcode2.ViewModels;
using Moq;
using Xunit;

namespace Devkit.Loading.Tests;

[Collection("Barcode2 environment")]
public sealed class ApiUpdateViewModelTests : IDisposable
{
    private readonly string _sourceDirectory;

    public ApiUpdateViewModelTests()
    {
        _sourceDirectory = Path.Combine(Path.GetTempPath(), $"devkit-loading-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sourceDirectory);
    }

    [Fact]
    public async Task All_four_buttons_use_page_loading_state()
    {
        var server = CreateSuccessfulServer();
        var notifications = CreateNotificationMock();
        var viewModel = CreateViewModel(server.Object, notifications);
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
        await viewModel.UpdateCommand.ExecuteAsync(Barcode2Environment.DevelopmentEnvironment);
        await viewModel.UpdateCommand.ExecuteAsync(Barcode2Environment.TestEnvironment);

        Assert.Equal(4, busyStarts);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
        Assert.Equal("execution", viewModel.SourceCodePreview);
        Assert.Equal("extension", viewModel.CodePreview);
        server.Verify(service => service.UpdateExtendBatch(
                It.Is<IReadOnlyCollection<ApiExtendUpdateRequest>>(updates =>
                    updates.Count == 1 &&
                    updates.Single().LookupKind == ApiLookupKind.Code &&
                    updates.Single().Identifier == "API001" &&
                    updates.Single().ExtendCode == "extension"),
                "first-connection"),
            Times.Once);
        server.Verify(service => service.UpdateExtendBatch(
                It.Is<IReadOnlyCollection<ApiExtendUpdateRequest>>(updates =>
                    updates.Count == 1 && updates.Single().Identifier == "API001"),
                "second-connection"),
            Times.Once);
        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Info)), Times.Exactly(2));
    }

    [Fact]
    public async Task Missing_target_reports_warning_and_resets_loading()
    {
        var server = CreateSuccessfulServer();
        var notifications = CreateNotificationMock();
        var viewModel = CreateViewModel(server.Object, notifications);
        viewModel.StrUpdateApis = "API001";

        await viewModel.UpdateCommand.ExecuteAsync(null);

        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Warning)), Times.Once);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
        server.Verify(
            service => service.UpdateExtendBatch(
                It.IsAny<IReadOnlyCollection<ApiExtendUpdateRequest>>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Server_failure_reports_error_and_resets_loading()
    {
        var server = CreateSuccessfulServer();
        server.Setup(service => service.UpdateExtendBatch(
                It.IsAny<IReadOnlyCollection<ApiExtendUpdateRequest>>(),
                It.IsAny<string>()))
            .Throws(new InvalidOperationException("database unavailable"));
        var notifications = CreateNotificationMock();
        var viewModel = CreateViewModel(server.Object, notifications);
        viewModel.StrUpdateApis = "API001";

        await viewModel.UpdateCommand.ExecuteAsync(Barcode2Environment.DevelopmentEnvironment);

        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Error &&
            request.Message.Contains("database unavailable"))), Times.Once);
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
        Assert.True(started?.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
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

    [Fact]
    public async Task Name_update_is_used_when_code_list_is_empty()
    {
        var server = CreateSuccessfulServer();
        var viewModel = CreateViewModel(server.Object);
        viewModel.StrUpdateApiNames = " Api One ; Api One ;Api Two ";

        await viewModel.UpdateCommand.ExecuteAsync(Barcode2Environment.DevelopmentEnvironment);

        server.Verify(service => service.UpdateExtendBatch(
                It.Is<IReadOnlyCollection<ApiExtendUpdateRequest>>(updates =>
                    updates.Select(update => update.Identifier).SequenceEqual(new[] { "Api One", "Api Two" }) &&
                    updates.All(update => update.LookupKind == ApiLookupKind.Name &&
                                                  update.ExtendCode == "name-extension")),
                "first-connection"),
            Times.Once);
    }

    [Fact]
    public async Task Code_update_ignores_name_list()
    {
        var server = CreateSuccessfulServer();
        var viewModel = CreateViewModel(server.Object);
        viewModel.StrUpdateApis = "API001";
        viewModel.StrUpdateApiNames = "Api One";

        await viewModel.UpdateCommand.ExecuteAsync(Barcode2Environment.DevelopmentEnvironment);

        server.Verify(service => service.GetSourceCodeByApiName(
            It.IsAny<List<ApiSourceInfo>>(), It.IsAny<string>()), Times.Never);
        server.Verify(service => service.UpdateExtendBatch(
                It.Is<IReadOnlyCollection<ApiExtendUpdateRequest>>(updates =>
                    updates.All(update => update.LookupKind == ApiLookupKind.Code)),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Preview_uses_name_only_when_code_is_empty()
    {
        var server = CreateSuccessfulServer();
        var viewModel = CreateViewModel(server.Object);
        viewModel.StrSelectApiName = "Api One";

        await viewModel.PreviewCommand.ExecuteAsync(null);

        Assert.Equal("name-execution", viewModel.SourceCodePreview);
        Assert.Equal("name-extension", viewModel.CodePreview);

        viewModel.StrSelectApiCodes = "API001";
        await viewModel.PreviewCommand.ExecuteAsync(null);

        Assert.Equal("execution", viewModel.SourceCodePreview);
        Assert.Equal("extension", viewModel.CodePreview);
        server.Verify(service => service.GetExecutionSourceCodeByApiName(
            _sourceDirectory, "Api One"), Times.Once);
    }

    public void Dispose()
    {
        Directory.Delete(_sourceDirectory, recursive: true);
    }

    private ApiUpdateViewModel CreateViewModel(
        IApiUpdateServer server,
        Mock<IClientNotificationService>? notifications = null)
    {
        var configuration = new Barcode2TestConfigurationService();
        configuration.Settings.SourceCodePath = _sourceDirectory;
        configuration.ConnectionStrings[Barcode2Defaults.DevelopmentEnvironment] = "first-connection";
        configuration.ConnectionStrings[Barcode2Defaults.TestEnvironment] = "second-connection";

        return new ApiUpdateViewModel(
            server,
            configuration,
            (notifications ?? CreateNotificationMock()).Object);
    }

    private static Mock<IClientNotificationService> CreateNotificationMock()
    {
        var notifications = new Mock<IClientNotificationService>();
        notifications.Setup(service => service.Show(It.IsAny<NotificationRequest>()))
            .Returns(() => Guid.NewGuid().ToString("N"));
        return notifications;
    }

    private Mock<IApiUpdateServer> CreateSuccessfulServer()
    {
        var server = new Mock<IApiUpdateServer>();
        var sourceInfo = new ApiSourceInfo { ApiCodes = ["API001"], ApiNames = ["Api One", "Api Two"] };
        server.Setup(service => service.GetAllApiSourceInfos(_sourceDirectory)).Returns([sourceInfo]);
        server.Setup(service => service.GetAllApiCodes(It.IsAny<List<ApiSourceInfo>>()))
            .Returns(["API001"]);
        server.Setup(service => service.GetExecutionSourceCodeByApiCode(_sourceDirectory, "API001"))
            .Returns("execution");
        server.Setup(service => service.GetExecutionSourceCodeByApiName(_sourceDirectory, "Api One"))
            .Returns("name-execution");
        server.Setup(service => service.GetSourceCodeByApiCode(_sourceDirectory, "API001"))
            .Returns("extension");
        server.Setup(service => service.GetSourceCodeByApiCode(It.IsAny<List<ApiSourceInfo>>(), "API001"))
            .Returns("extension");
        server.Setup(service => service.GetSourceCodeByApiName(_sourceDirectory, "Api One"))
            .Returns("name-extension");
        server.Setup(service => service.GetSourceCodeByApiName(
                It.IsAny<List<ApiSourceInfo>>(), It.IsAny<string>()))
            .Returns("name-extension");
        server.Setup(service => service.UpdateExtendBatch(
                It.IsAny<IReadOnlyCollection<ApiExtendUpdateRequest>>(),
                It.IsAny<string>()))
            .Returns(true);
        return server;
    }
}

[CollectionDefinition("Barcode2 environment", DisableParallelization = true)]
public sealed class Barcode2EnvironmentCollection
{
}
