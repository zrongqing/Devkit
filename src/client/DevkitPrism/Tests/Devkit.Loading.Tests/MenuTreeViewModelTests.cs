using System.Net.Http;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Ssamc.Configuration;
using Ssamc.Models;
using Ssamc.Servers;
using Devkit.Services.Interfaces;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Ssamc.ViewModels;
using Moq;
using Ssamc.DB;
using Ssamc.DB.Entities;
using Xunit;

namespace Devkit.Loading.Tests;

[Collection("Ssamc environment")]
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
        var viewModel = CreateViewModel(CreateMenuTree(), launcher.Object);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        var swagger = viewModel.TreeNodes[0].Children[0];
        viewModel.SelectedEnvironment = viewModel.EnvironmentOptions.Single(environment =>
            environment.Key == SsamcEnvironment.ProductionEnvironment);

        await viewModel.OpenCommand.ExecuteAsync(swagger);

        launcher.Verify(service => service.OpenTabAsync(
                It.Is<WebTabOpenRequest>(request =>
                    request.Title == "Swagger" &&
                    request.BaseAddress == viewModel.SelectedEnvironment.BaseAddress &&
                    request.MainPageId == 20001),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
        var viewModel = CreateViewModel(source.Object);

        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal("menu server unavailable", viewModel.LastNotificationMessage);
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
        var viewModel = CreateViewModel(CreateMenuTree(), launcher.Object);
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        await viewModel.OpenCommand.ExecuteAsync(viewModel.TreeNodes[0].Children[0]);

        Assert.Equal("browser unavailable", viewModel.LastNotificationMessage);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
    }

    [Fact]
    public async Task Selected_environment_is_persisted_between_view_models()
    {
        var files = new MemoryFileService();
        var first = CreateViewModel(CreateMenuTree(), files: files);
        first.SelectedEnvironment = first.EnvironmentOptions.Single(environment =>
            environment.Key == SsamcEnvironment.TestEnvironment);

        var second = CreateViewModel(CreateMenuTree(), files: files);
        await second.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SsamcEnvironment.TestEnvironment, second.SelectedEnvironment?.Key);
    }

    [Fact]
    public void Deep_link_uses_the_requested_main_page_as_moduleid()
    {
        var address = SystemWebPageLauncher.BuildDeepLink(new WebTabOpenRequest(
            "菜单 标题",
            "http://192.168.10.41/base",
            1435766981386702849));

        Assert.Equal(
            "http://192.168.10.41/Page/?moduleid=1435766981386702849",
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
    public void Page_environments_have_the_required_defaults_and_legacy_menu_connection_is_supported()
    {
        const string menuConnectionName = "DEVKIT_SSAMC_MENU_DB_CONNECTION";
        var names = new[]
        {
            "DEVKIT_SSAMC_PAGE_PRODUCTION_BASE_URL",
            "DEVKIT_SSAMC_PAGE_TEST_BASE_URL",
            "DEVKIT_SSAMC_PAGE_DEVELOPMENT_BASE_URL"
        };
        var originals = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        var originalMenuConnection = Environment.GetEnvironmentVariable(menuConnectionName);

        try
        {
            foreach (var name in names)
            {
                Environment.SetEnvironmentVariable(name, null);
            }

            Environment.SetEnvironmentVariable(menuConnectionName, "menu-connection");
            var environments = SsamcEnvironment.GetPageEnvironments();

            Assert.Equal("http://192.168.10.41", environments[0].BaseAddress);
            Assert.Equal("http://192.168.20.54", environments[1].BaseAddress);
            Assert.Equal("http://192.168.215.57", environments[2].BaseAddress);
            Assert.Equal(
                "menu-connection",
                SsamcEnvironment.GetMenuDatabaseConnection(SsamcEnvironment.DevelopmentEnvironment));
        }
        finally
        {
            foreach (var pair in originals)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            Environment.SetEnvironmentVariable(menuConnectionName, originalMenuConnection);
        }
    }

    [Fact]
    public void Menu_database_connection_uses_the_selected_environment_defaults()
    {
        var names = new[]
        {
            "DEVKIT_SSAMC_MENU_DB_CONNECTION",
            "DEVKIT_SSAMC_MENU_DB_PRODUCTION_CONNECTION",
            "DEVKIT_SSAMC_MENU_DB_TEST_CONNECTION",
            "DEVKIT_SSAMC_MENU_DB_DEVELOPMENT_CONNECTION"
        };
        var originals = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);

        try
        {
            foreach (var name in names)
            {
                Environment.SetEnvironmentVariable(name, null);
            }

            Assert.Contains(
                "192.168.10.68/ssamcerp",
                SsamcEnvironment.GetMenuDatabaseConnection(SsamcEnvironment.ProductionEnvironment));
            Assert.Contains(
                "192.168.20.54/ssamcerp",
                SsamcEnvironment.GetMenuDatabaseConnection(SsamcEnvironment.TestEnvironment));
            Assert.Contains(
                "192.168.215.57/ssamcerp",
                SsamcEnvironment.GetMenuDatabaseConnection(SsamcEnvironment.DevelopmentEnvironment));
        }
        finally
        {
            foreach (var pair in originals)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }

    [Fact]
    public void Menu_database_connection_prefers_the_configuration_file()
    {
        var files = new MemoryFileService();
        files.Save(
            "settings",
            "menu-tree.json",
            new MenuTreeSettings
            {
                DatabaseConnections = new Dictionary<string, string>
                {
                    [SsamcEnvironment.TestEnvironment] = "configured-test-connection"
                }
            });
        var storage = new Mock<IModuleStorage>();
        storage.Setup(service => service.GetModulePath("ssamc")).Returns("settings");
        var source = new OracleMenuTreeDataSource(files, storage.Object);

        var connection = source.ResolveConnectionString(SsamcEnvironment.TestEnvironment);

        Assert.Equal("configured-test-connection", connection);
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
            environment.Key == SsamcEnvironment.ProductionEnvironment);

        await viewModel.ReloadCommand.ExecuteAsync(null);

        source.Verify(service => service.GetMenuTreeAsync(
            SsamcEnvironment.ProductionEnvironment,
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
        IFileService? files = null)
    {
        var source = new Mock<IMenuTreeDataSource>();
        source.Setup(service => service.GetMenuTreeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        return CreateViewModel(source.Object, launcher, files);
    }

    private static MenuTreeViewModel CreateViewModel(
        IMenuTreeDataSource source,
        IWebPageLauncher? launcher = null,
        IFileService? files = null)
    {
        var storage = new Mock<IModuleStorage>();
        storage.Setup(service => service.GetModulePath("ssamc")).Returns("settings");
        return new MenuTreeViewModel(
            source,
            launcher ?? Mock.Of<IWebPageLauncher>(),
            files ?? Mock.Of<IFileService>(),
            storage.Object);
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

    private sealed class MemoryFileService : IFileService
    {
        private object? _content;

        public T? Read<T>(string folderPath, string fileName) =>
            _content is T value ? value : default;

        public void Save<T>(string folderPath, string fileName, T content) =>
            _content = content;

        public void Delete(string folderPath, string fileName) => _content = null;
    }
}
