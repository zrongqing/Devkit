using System.Net.Http;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Barcode2.Configuration;
using Barcode2.Models;
using Barcode2.Servers;
using Devkit.Services.Interfaces.Notifications;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Barcode2.ViewModels;
using Moq;
using Barcode2.DB;
using Barcode2.DB.Context;
using Barcode2.DB.Entities;
using Xunit;

namespace Devkit.Loading.Tests;

[Collection("Barcode2 environment")]
public sealed class MenuTreeViewModelTests
{
    [Fact]
    public async Task Search_runs_on_command_and_keeps_the_matching_hierarchy()
    {
        var viewModel = CreateViewModel(CreateMenuTree());
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        viewModel.SearchText = "swagger-code";
        Assert.Equal(2, Assert.Single(viewModel.TreeNodes).Children.Count);

        viewModel.SearchCommand.Execute(null);

        var root = Assert.Single(viewModel.TreeNodes);
        Assert.Equal("Platform", root.Name);
        Assert.True(root.IsExpanded);
        Assert.Equal("Swagger", Assert.Single(root.Children).Name);
    }

    [Fact]
    public async Task Search_matches_module_fields_and_parent_match_keeps_all_children()
    {
        var viewModel = CreateViewModel(CreateMenuTree());
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        viewModel.SearchText = "platform";
        viewModel.SearchCommand.Execute(null);
        Assert.Equal(2, Assert.Single(viewModel.TreeNodes).Children.Count);

        viewModel.SearchText = "10002";
        viewModel.SearchCommand.Execute(null);
        Assert.Equal("Logs", Assert.Single(Assert.Single(viewModel.TreeNodes).Children).Name);
    }

    [Fact]
    public async Task Selection_populates_menu_module_and_main_page_details()
    {
        var viewModel = CreateViewModel(CreateMenuTree());
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        var swagger = viewModel.TreeNodes[0].Children[0];

        viewModel.SelectedMenu = swagger;

        Assert.NotNull(viewModel.SelectedDetails);
        Assert.Equal(2, viewModel.SelectedDetails.MenuId);
        Assert.Equal("Swagger", viewModel.SelectedDetails.MenuName);
        Assert.Equal("swagger-code", viewModel.SelectedDetails.MenuCode);
        Assert.Equal("Platform", viewModel.SelectedDetails.ParentDirectory);
        Assert.Equal("Swagger module", viewModel.SelectedDetails.ModuleName);
        Assert.Equal("10001", viewModel.SelectedDetails.ModuleId);
        Assert.Equal("20001", viewModel.SelectedDetails.MainPageId);
        Assert.Equal("Swagger main page", viewModel.SelectedDetails.MainPageName);
    }

    [Fact]
    public async Task Changing_selection_replaces_module_and_main_page_details()
    {
        var viewModel = CreateViewModel(CreateMenuTree());
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        var menus = viewModel.TreeNodes[0].Children;

        viewModel.SelectedMenu = menus[0];
        var firstDetails = viewModel.SelectedDetails;
        viewModel.SelectedMenu = menus[1];

        Assert.NotSame(firstDetails, viewModel.SelectedDetails);
        Assert.Equal("Logs module", viewModel.SelectedDetails?.ModuleName);
        Assert.Equal("20002", viewModel.SelectedDetails?.MainPageId);
        Assert.Equal("Logs main page", viewModel.SelectedDetails?.MainPageName);
    }

    [Fact]
    public void Detail_fields_are_grouped_in_the_requested_order()
    {
        var properties = typeof(MenuDetails)
            .GetProperties()
            .Select(property => new
            {
                Category = property.GetCustomAttribute<CategoryAttribute>()?.Category,
                Order = property.GetCustomAttribute<DisplayAttribute>()?.GetOrder()
            })
            .OrderBy(property => property.Order)
            .ToArray();

        Assert.All(properties, property => Assert.NotNull(property.Order));
        Assert.Equal(
            ["菜单信息", "菜单模块", "模块主页面"],
            properties.Select(property => property.Category).Distinct());
    }

    [Fact]
    public async Task Open_delegates_the_selected_environment_and_main_page_to_the_launcher()
    {
        var launcher = new Mock<IWebPageLauncher>();
        var notifications = CreateNotificationMock();
        var viewModel = CreateViewModel(CreateMenuTree(), launcher.Object, notifications: notifications);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        var swagger = viewModel.TreeNodes[0].Children[0];
        viewModel.SelectedEnvironment = viewModel.EnvironmentOptions.Single(environment =>
            environment.Key == Barcode2Environment.ProductionEnvironment);

        await viewModel.OpenCommand.ExecuteAsync(swagger);

        launcher.Verify(service => service.OpenTabAsync(
                It.Is<WebTabOpenRequest>(request =>
                    request.Title == "Swagger" &&
                    request.BaseAddress == viewModel.SelectedEnvironment.BaseAddress &&
                    request.MainPageId == 20001),
                It.IsAny<CancellationToken>()),
            Times.Once);
        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Info &&
            request.Message.Contains("Swagger"))), Times.Once);
    }

    [Fact]
    public async Task Open_with_missing_page_address_reports_configuration_warning()
    {
        var launcher = new Mock<IWebPageLauncher>();
        var notifications = CreateNotificationMock();
        var configuration = new Barcode2TestConfigurationService();
        configuration.Settings
            .GetEnvironment(Barcode2Environment.ProductionEnvironment)
            .PageBaseAddress = string.Empty;
        var viewModel = CreateViewModel(
            CreateMenuTree(),
            launcher.Object,
            configuration,
            notifications);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedEnvironment = viewModel.EnvironmentOptions.Single(environment =>
            environment.Key == Barcode2Environment.ProductionEnvironment);

        await viewModel.OpenCommand.ExecuteAsync(viewModel.TreeNodes[0].Children[0]);

        launcher.Verify(service => service.OpenTabAsync(
            It.IsAny<WebTabOpenRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Warning &&
            request.Message.Contains("页面/后端地址未配置"))), Times.Once);
    }

    [Fact]
    public async Task A_directory_without_module_cannot_be_opened()
    {
        var launcher = new Mock<IWebPageLauncher>();
        var viewModel = CreateViewModel(CreateMenuTree(), launcher.Object);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        var directory = viewModel.TreeNodes[0];

        Assert.False(viewModel.OpenCommand.CanExecute(directory));
        launcher.Verify(
            service => service.OpenTabAsync(
                It.IsAny<WebTabOpenRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_module_without_main_page_cannot_be_opened()
    {
        var launcher = new Mock<IWebPageLauncher>();
        var viewModel = CreateViewModel(
        [
            new MenuTreeItem
            {
                MenuId = 1,
                Name = "Module without main page",
                ModuleId = 10001,
                ModuleName = "Configured module"
            }
        ], launcher.Object);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        var menu = Assert.Single(viewModel.TreeNodes);

        Assert.False(viewModel.OpenCommand.CanExecute(menu));
        launcher.Verify(
            service => service.OpenTabAsync(
                It.IsAny<WebTabOpenRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Data_source_failure_is_reported_and_loading_is_reset()
    {
        var source = new Mock<IMenuTreeDataSource>();
        source.Setup(service => service.GetMenuTreeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("menu server unavailable"));
        var notifications = CreateNotificationMock();
        var viewModel = CreateViewModel(source.Object, notifications: notifications);

        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Error &&
            request.Message == "menu server unavailable")), Times.Once);
        Assert.Empty(viewModel.TreeNodes);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
    }

    [Fact]
    public async Task Launcher_failure_is_reported_and_loading_is_reset()
    {
        var launcher = new Mock<IWebPageLauncher>();
        launcher.Setup(service => service.OpenTabAsync(
                It.IsAny<WebTabOpenRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("browser unavailable"));
        var notifications = CreateNotificationMock();
        var viewModel = CreateViewModel(CreateMenuTree(), launcher.Object, notifications: notifications);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        await viewModel.OpenCommand.ExecuteAsync(viewModel.TreeNodes[0].Children[0]);

        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Error &&
            request.Message == "browser unavailable")), Times.Once);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
    }

    [Fact]
    public async Task Selected_environment_is_persisted_between_view_models()
    {
        var configuration = new Barcode2TestConfigurationService();
        var first = CreateViewModel(CreateMenuTree(), configuration: configuration);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        first.SelectedEnvironment = first.EnvironmentOptions.Single(environment =>
            environment.Key == Barcode2Environment.TestEnvironment);
        await first.ReloadCommand.ExecuteAsync(null);

        var second = CreateViewModel(CreateMenuTree(), configuration: configuration);
        await second.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Barcode2Environment.TestEnvironment, second.SelectedEnvironment?.Key);
    }

    [Fact]
    public void Deep_link_uses_the_requested_main_page_as_moduleid()
    {
        var address = SystemWebPageLauncher.BuildDeepLink(new WebTabOpenRequest(
            "菜单 标题",
            "https://app.example.invalid/base",
            1435766981386702849));

        Assert.Equal(
            "https://app.example.invalid/Page/?moduleid=1435766981386702849",
            address.AbsoluteUri);
    }

    [Fact]
    public void Invalid_page_environment_is_rejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SystemWebPageLauncher.BuildDeepLink(new WebTabOpenRequest(
                "Menu",
                "file:///c:/temp",
                1)));

        Assert.Contains("页面环境地址无效", exception.Message);
    }

    [Fact]
    public async Task Reload_passes_the_selected_environment_to_the_data_source()
    {
        var source = new Mock<IMenuTreeDataSource>();
        source.Setup(service => service.GetMenuTreeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMenuTree());
        var viewModel = CreateViewModel(source.Object);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedEnvironment = viewModel.EnvironmentOptions.Single(environment =>
            environment.Key == Barcode2Environment.ProductionEnvironment);

        await viewModel.ReloadCommand.ExecuteAsync(null);

        source.Verify(service => service.GetMenuTreeAsync(
            Barcode2Environment.ProductionEnvironment,
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public void Oracle_delete_flag_is_numeric()
    {
        Assert.Equal(typeof(short?), typeof(SYS_MENU).GetProperty(nameof(SYS_MENU.IS_DELETE))?.PropertyType);
    }

    [Fact]
    public void Oracle_query_row_uses_the_menu_record_mapster_configuration()
    {
        var row = new MenuTreeQueryRow
        {
            MenuId = 12,
            Name = "Mapped menu",
            ModuleId = 34,
            ModuleName = "Mapped module",
            MainPageId = 56,
            MainPageName = "Mapped main page"
        };

        var record = row.Adapt<MenuTreeRecord>(MenuTreeRecordMapping.Configuration);

        Assert.Equal(row.MenuId, record.MenuId);
        Assert.Equal(row.Name, record.Name);
        Assert.Equal(row.ModuleId, record.ModuleId);
        Assert.Equal(row.ModuleName, record.ModuleName);
        Assert.Equal(row.MainPageId, record.MainPageId);
        Assert.Equal(row.MainPageName, record.MainPageName);
    }

    [Fact]
    public void Oracle_query_filters_numeric_delete_flag_and_does_not_filter_state()
    {
        using var context = new MyDbContext(
            "User Id=test;Password=test;Data Source=localhost/test");

        var sql = OracleMenuTreeDataSource.CreateQuery(context).ToQueryString();

        Assert.Contains("IS_DELETE", sql);
        Assert.Contains("= 0", sql);
        Assert.DoesNotContain("IS_STATE", sql);
        Assert.Contains("SYS_MODULE", sql);
        Assert.Contains("SYS_PAGE", sql);
        Assert.Contains("IS_MAIN", sql);
    }

    private static MenuTreeViewModel CreateViewModel(
        IReadOnlyList<MenuTreeItem> items,
        IWebPageLauncher? launcher = null,
        IBarcode2ConfigurationService? configuration = null,
        Mock<IClientNotificationService>? notifications = null)
    {
        var source = new Mock<IMenuTreeDataSource>();
        source.Setup(service => service.GetMenuTreeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        return CreateViewModel(source.Object, launcher, configuration, notifications);
    }

    private static MenuTreeViewModel CreateViewModel(
        IMenuTreeDataSource source,
        IWebPageLauncher? launcher = null,
        IBarcode2ConfigurationService? configuration = null,
        Mock<IClientNotificationService>? notifications = null)
    {
        return new MenuTreeViewModel(
            source,
            launcher ?? Mock.Of<IWebPageLauncher>(),
            configuration ?? new Barcode2TestConfigurationService(),
            (notifications ?? CreateNotificationMock()).Object);
    }

    private static Mock<IClientNotificationService> CreateNotificationMock()
    {
        var notifications = new Mock<IClientNotificationService>();
        notifications.Setup(service => service.Show(It.IsAny<NotificationRequest>()))
            .Returns(() => Guid.NewGuid().ToString("N"));
        return notifications;
    }

    private static IReadOnlyList<MenuTreeItem> CreateMenuTree()
    {
        return
        [
            new MenuTreeItem
            {
                MenuId = 1,
                Name = "Platform",
                Code = "platform-code",
                Children =
                [
                    new MenuTreeItem
                    {
                        MenuId = 2,
                        ParentMenuId = 1,
                        Name = "Swagger",
                        Code = "swagger-code",
                        ParentName = "Platform",
                        ModuleId = 10001,
                        ModuleName = "Swagger module",
                        MainPageId = 20001,
                        MainPageName = "Swagger main page"
                    },
                    new MenuTreeItem
                    {
                        MenuId = 3,
                        ParentMenuId = 1,
                        Name = "Logs",
                        Code = "logs-code",
                        ParentName = "Platform",
                        ModuleId = 10002,
                        ModuleName = "Logs module",
                        MainPageId = 20002,
                        MainPageName = "Logs main page"
                    }
                ]
            }
        ];
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => Uri.UnescapeDataString(part[0]),
                part => Uri.UnescapeDataString(part[1]));
    }

}
