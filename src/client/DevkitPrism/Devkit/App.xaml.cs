using System.Net.Http;
using System.IO;
using System.Windows;
using Devkit.Core;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Mvvm;
using Devkit.Core.UI.Services;
using Devkit.Prism;
using Devkit.Prism.Extensions;
using Devkit.Services;
using Devkit.Services.Diagnostics;
using Devkit.Services.Interfaces.Logging;
using Devkit.Services.Logging;
using Devkit.Services.Interfaces;
using Devkit.ViewModels;
using Devkit.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Licensing;

namespace Devkit;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : DevkitPrismApplication
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ClientCrashHandler _crashHandler;

    public App()
    {
        _loggerFactory = ClientLoggingExtensions.CreateBootstrapLoggerFactory();
        _crashHandler = new ClientCrashHandler(new ClientLogger(_loggerFactory.CreateLogger<ClientLogger>()));

        DispatcherUnhandledException += _crashHandler.HandleDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += _crashHandler.HandleAppDomainException;
        TaskScheduler.UnobservedTaskException += _crashHandler.HandleUnobservedTaskException;

        // Add your Syncfusion license key for WPF platform with corresponding Syncfusion NuGet version referred in project.
        var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")
                         ?? Environment.GetEnvironmentVariable("SYNFUSION_LICENSE_KEY");
        SyncfusionLicenseProvider.RegisterLicense(licenseKey);
    }

    public T? GetService<T>()
        where T : class
    {
        return _containerProvider.Resolve<T>() as T;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        ConfigureServices(GetPrismServiceCollection());
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= _crashHandler.HandleDispatcherException;
        AppDomain.CurrentDomain.UnhandledException -= _crashHandler.HandleAppDomainException;
        TaskScheduler.UnobservedTaskException -= _crashHandler.HandleUnobservedTaskException;
        _loggerFactory.Dispose();
        base.OnExit(e);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddClientLogging();
        var apiBaseUrl = Environment.GetEnvironmentVariable("DEVKIT_API_BASE_URL") ?? "http://localhost:5000/";
        services.AddSingleton(new HttpClient { BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute) });
        services.AddSingleton<ISystemInfoClient, SystemInfoClient>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IModuleStorage, ModuleStorage>();
        services.AddSingleton<IMessageService, MessageService>();
        services.AddSingleton<DelayedLoadingState>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IMenuRegistry, MenuRegistry>();
        services.AddSingleton<IRemoteMenuConfigurationClient, RemoteMenuConfigurationClient>();
    }

    protected override Window CreateShell()
    {
        var regionManager = Container.Resolve<IRegionManager>();
        regionManager.RegisterViewWithRegion(RegionNames.MenuRegion, typeof(MenuView));
        regionManager.RegisterViewWithRegion(RegionNames.MenuTabRegion, typeof(MenuTabView));

        LoadMenus();

        return Container.Resolve<ShellWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IShellService, ShellService>();
        containerRegistry.RegisterForNavigation<MenuView, MenuViewModel>(SysViewKeys.Menu);
        containerRegistry.RegisterForNavigation<MenuTabView, MenuTabViewModel>(SysViewKeys.MenuTab);
        containerRegistry.RegisterForNavigation<HomeView, HomeViewModel>("HomeView");
        containerRegistry.RegisterForNavigation<SettingView, SettingViewModel>("SettingView");
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModulesFromDirectory(Path.Combine(AppContext.BaseDirectory, "modules"));
    }

    private void LoadMenus()
    {
        var menuRegistry = Container.Resolve<IMenuRegistry>();
        menuRegistry.ScanFromAssembly(GetType().Assembly);

        menuRegistry.Register(new MenuItemModel
        {
            Id = "home",
            ParentId = null,
            Title = "首页",
            Order = 0,
            ViewName = "HomeView",
            IsClosable = false
        });

        menuRegistry.Register(new MenuItemModel
        {
            Id = "modules",
            ParentId = null,
            Title = "模块",
            Order = 10
        });

        menuRegistry.Register(new MenuItemModel
        {
            Id = "settings",
            ParentId = null,
            Title = "设置",
            Order = 90,
            ViewName = "SettingView",
            AllowMultipleTabs = false
        });

        LoadRemoteMenus(menuRegistry);
    }

    private void LoadRemoteMenus(IMenuRegistry menuRegistry)
    {
        try
        {
            var remoteMenuClient = Container.Resolve<IRemoteMenuConfigurationClient>();
            var remoteMenus = remoteMenuClient.GetMenusAsync().GetAwaiter().GetResult();
            menuRegistry.RegisterRemoteRange(remoteMenus);
        }
        catch (Exception exception)
        {
            GetService<IClientLogger>()?.Warning(exception, "Remote menu configuration is unavailable.");
        }
    }
}
