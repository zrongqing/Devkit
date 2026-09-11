using System.IO;
using Devkit.Services.Interfaces.Notifications;
using Moq;
using Barcode2.Configuration;
using Barcode2.ViewModels;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class Barcode2SettingsViewModelTests
{
    [Fact]
    public async Task Complete_configuration_loads_and_saves()
    {
        var configuration = new Barcode2TestConfigurationService();
        var notifications = CreateNotifications();
        var viewModel = new Barcode2SettingsViewModel(
            configuration,
            CreateConnectionTester().Object,
            notifications.Object);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        viewModel.SourceCodePath = "new-api-path";
        viewModel.WebappSourcePath = "new-webapp-path";
        viewModel.Environments[0].DatabasePassword = "changed-password";
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("new-api-path", configuration.Settings.SourceCodePath);
        Assert.Equal("new-webapp-path", configuration.Settings.WebappSourcePath);
        Assert.Equal(
            "changed-password",
            configuration.Settings.Environments[0].DatabasePassword);
        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Info && request.Message.Contains("已保存"))), Times.Once);
    }

    [Fact]
    public async Task Partial_configuration_can_be_saved()
    {
        var configuration = new Barcode2TestConfigurationService();
        var notifications = CreateNotifications();
        var viewModel = new Barcode2SettingsViewModel(
            configuration,
            CreateConnectionTester().Object,
            notifications.Object);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        viewModel.Environments[0].DatabasePassword = string.Empty;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(configuration.Settings.Environments[0].DatabasePassword);
        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Info && request.Message.Contains("已保存"))), Times.Once);
    }

    [Fact]
    public async Task Storage_failure_reports_error_and_resets_loading()
    {
        var configuration = new Mock<IBarcode2ConfigurationService>();
        configuration.Setup(service => service.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Barcode2Defaults.Create());
        configuration.Setup(service => service.SaveAsync(
                It.IsAny<Barcode2Settings>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("sqlite unavailable"));
        var notifications = CreateNotifications();
        var viewModel = new Barcode2SettingsViewModel(
            configuration.Object,
            CreateConnectionTester().Object,
            notifications.Object);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        await viewModel.SaveCommand.ExecuteAsync(null);

        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Error &&
            request.Message.Contains("sqlite unavailable"))), Times.Once);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
    }

    [Fact]
    public async Task Environment_detection_uses_current_editor_values_and_reports_success()
    {
        var configuration = new Barcode2TestConfigurationService();
        var tester = CreateConnectionTester();
        Barcode2EnvironmentSettings? tested = null;
        tester.Setup(service => service.TestEnvironmentAsync(
                It.IsAny<Barcode2EnvironmentSettings>(),
                It.IsAny<CancellationToken>()))
            .Callback<Barcode2EnvironmentSettings, CancellationToken>((settings, _) => tested = settings)
            .Returns(Task.CompletedTask);
        var notifications = CreateNotifications();
        var viewModel = new Barcode2SettingsViewModel(
            configuration,
            tester.Object,
            notifications.Object);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        var editor = viewModel.Environments[0];
        editor.DatabaseUsername = "current-user";
        editor.DatabasePassword = "current-password";

        await viewModel.TestEnvironmentCommand.ExecuteAsync(editor);

        Assert.NotNull(tested);
        Assert.Equal("current-user", tested.DatabaseUsername);
        Assert.Equal("current-password", tested.DatabasePassword);
        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Info &&
            request.Message.Contains("服务器及数据库连接正常"))), Times.Once);
    }

    [Fact]
    public async Task Share_detection_uses_independent_credentials_and_reports_success()
    {
        var configuration = new Barcode2TestConfigurationService();
        var tester = CreateConnectionTester();
        Barcode2ShareSettings? tested = null;
        tester.Setup(service => service.TestShareAsync(
                It.IsAny<Barcode2ShareSettings>(),
                It.IsAny<CancellationToken>()))
            .Callback<Barcode2ShareSettings, CancellationToken>((settings, _) => tested = settings)
            .Returns(Task.CompletedTask);
        var notifications = CreateNotifications();
        var viewModel = new Barcode2SettingsViewModel(
            configuration,
            tester.Object,
            notifications.Object);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        var editor = viewModel.ShareTargets.First(target => target.TargetKey == "production");
        editor.Username = "target-user";
        editor.Password = "target-password";

        await viewModel.TestShareCommand.ExecuteAsync(editor);

        Assert.NotNull(tested);
        Assert.Equal(editor.Id, tested.Id);
        Assert.Equal("target-user", tested.Username);
        Assert.Equal("target-password", tested.Password);
        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Info &&
            request.Message.Contains("发布目录连接正常"))), Times.Once);
    }

    [Fact]
    public async Task Detection_failure_reports_error_and_resets_loading()
    {
        var configuration = new Barcode2TestConfigurationService();
        var tester = CreateConnectionTester();
        tester.Setup(service => service.TestEnvironmentAsync(
                It.IsAny<Barcode2EnvironmentSettings>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("连接失败，请检查配置。"));
        var notifications = CreateNotifications();
        var viewModel = new Barcode2SettingsViewModel(
            configuration,
            tester.Object,
            notifications.Object);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        await viewModel.TestEnvironmentCommand.ExecuteAsync(viewModel.Environments[0]);

        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Error && request.Message.Contains("连接失败"))), Times.Once);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
    }

    private static Mock<IBarcode2ConnectionTester> CreateConnectionTester()
    {
        var tester = new Mock<IBarcode2ConnectionTester>();
        tester.Setup(service => service.TestEnvironmentAsync(
                It.IsAny<Barcode2EnvironmentSettings>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        tester.Setup(service => service.TestShareAsync(
                It.IsAny<Barcode2ShareSettings>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return tester;
    }

    private static Mock<IClientNotificationService> CreateNotifications()
    {
        var notifications = new Mock<IClientNotificationService>();
        notifications.Setup(service => service.Show(It.IsAny<NotificationRequest>()))
            .Returns(() => Guid.NewGuid().ToString("N"));
        return notifications;
    }
}
