using System.Net.Http;
using System.Windows;
using Devkit.Core;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Mvvm;
using Devkit.Core.UI.Services;
using Devkit.Modules;
using Devkit.Modules.ModuleManagement;
using Devkit.Prism;
using Devkit.Prism.Modules;
using Devkit.Services;
using Devkit.Services.Authentication;
using Devkit.Services.Diagnostics;
using Devkit.Services.Dialogs;
using Devkit.Services.Configuration;
using Devkit.Services.Interfaces.Configuration;
using Devkit.Services.Interfaces.Dialogs;
using Devkit.Services.Interfaces.Logging;
using Devkit.Services.Interfaces.Notifications;
using Devkit.Services.Logging;
using Devkit.Services.Interfaces;
using Devkit.Services.Interfaces.Authentication;
using Devkit.Services.Notifications;
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
    private const string SingleInstanceMutexName = @"Local\Devkit.Client.SingleInstance";
    private const string SingleInstanceActivationEventName = @"Local\Devkit.Client.Activate";

    private readonly ILoggerFactory _loggerFactory;
    private readonly ClientCrashHandler _crashHandler;
    private readonly CancellationTokenSource _shutdownCancellation = new();
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _singleInstanceActivationEvent;
    private RegisteredWaitHandle? _singleInstanceActivationRegistration;
    private bool _ownsSingleInstanceMutex;

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
        return _containerProvider?.Resolve<T>() as T;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!TryAcquireSingleInstance())
        {
            Shutdown();
            return;
        }

        ConfigureServices(GetPrismServiceCollection());
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdownCancellation.Cancel();
        GetService<DynamicModuleManager>()?.Shutdown();
        GetService<IClientNotificationService>()?.CloseAll();
        DispatcherUnhandledException -= _crashHandler.HandleDispatcherException;
        AppDomain.CurrentDomain.UnhandledException -= _crashHandler.HandleAppDomainException;
        TaskScheduler.UnobservedTaskException -= _crashHandler.HandleUnobservedTaskException;
        _singleInstanceActivationRegistration?.Unregister(null);
        _singleInstanceActivationEvent?.Dispose();
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        _shutdownCancellation.Dispose();
        _loggerFactory.Dispose();
        base.OnExit(e);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddClientLogging();
        var apiBaseUrl = Environment.GetEnvironmentVariable("DEVKIT_API_BASE_URL") ?? "http://localhost:12511/";
        services.AddSingleton(new HttpClient { BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute) });
        services.AddSingleton<ISystemInfoClient, SystemInfoClient>();
        services.AddSingleton<IAuthClient, AuthClient>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IModuleStorage, ModuleStorage>();
        services.AddSingleton<ILocalSettingsStore>(_ =>
            new SqliteLocalSettingsStore(SqliteLocalSettingsStore.GetDefaultDatabasePath()));
        services.AddSingleton<IMessageService, MessageService>();
        services.AddSingleton<IClientUiContext, WpfClientUiContext>();
        services.AddSingleton<IToastNotificationPresenter, SyncfusionToastNotificationPresenter>();
        services.AddSingleton<IWindowsToastRegistration, WindowsToastRegistration>();
        services.AddSingleton<IClientNotificationService, ClientNotificationService>();
        services.AddSingleton<IConfirmationDialogPresenter, ConfirmationDialogPresenter>();
        services.AddSingleton<IConfirmationDialogService, ConfirmationDialogService>();
        services.AddSingleton<DelayedLoadingState>();
        services.AddSingleton<IMenuRegistry, MenuRegistry>();
        services.AddSingleton<IRemoteMenuConfigurationClient, RemoteMenuConfigurationClient>();
        services.AddSingleton<IRemoteMenuConfigurationCache, LocalRemoteMenuConfigurationCache>();
    }

    protected override Window CreateShell()
    {
        var regionManager = Container.Resolve<IRegionManager>();
        regionManager.RegisterViewWithRegion(RegionNames.MenuRegion, typeof(MenuView));
        regionManager.RegisterViewWithRegion(RegionNames.MenuTabRegion, typeof(MenuTabView));

        LoadMenus();

        Container.Resolve<IWindowsToastRegistration>().EnsureRegistered();

        var shell = Container.Resolve<ShellWindow>();
        shell.ContentRendered += OnShellContentRendered;
        return shell;
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IPrismModuleLoader, PrismModuleLoader>();
        containerRegistry.RegisterSingleton<DynamicModuleManager, DynamicModuleManager>();
        containerRegistry.RegisterSingleton<IModuleContentCoordinator, ModuleContentCoordinator>();
        containerRegistry.RegisterSingleton<IShellService, ShellService>();
        containerRegistry.RegisterForNavigation<MenuView, MenuViewModel>(SysViewKeys.Menu);
        containerRegistry.RegisterForNavigation<MenuTabView, MenuTabViewModel>(SysViewKeys.MenuTab);
        containerRegistry.RegisterForNavigation<HomeView, HomeViewModel>("HomeView");
        containerRegistry.RegisterForNavigation<SettingView, SettingViewModel>("SettingView");
        containerRegistry.RegisterForNavigation<SystemStatusView, SystemStatusViewModel>("SystemStatusView");
        containerRegistry.RegisterForNavigation<AboutView, AboutViewModel>("AboutView");
        containerRegistry.RegisterForNavigation<UnavailableView, UnavailableViewModel>("UnavailableView");
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<ModuleManagementModule>();
    }

    /// <summary>
    /// 加载菜单
    /// </summary>
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
            Id = "dev",
            ParentId = null,
            Title = "dev",
            Order = -100
        });

        //menuRegistry.Register(new MenuItemModel
        //{
        //    Id = "settings",
        //    ParentId = null,
        //    Title = "设置",
        //    Order = 90,
        //    ViewName = "SettingView",
        //    AllowMultipleTabs = false
        //});

    }

    private void OnShellContentRendered(object? sender, EventArgs eventArgs)
    {
        if (sender is Window shell)
        {
            shell.ContentRendered -= OnShellContentRendered;
        }

        _ = LoadRemoteMenusAsync(_shutdownCancellation.Token);
    }

    private bool TryAcquireSingleInstance()
    {
        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            SingleInstanceMutexName,
            out var createdNew);
        _singleInstanceActivationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            SingleInstanceActivationEventName);

        if (!createdNew)
        {
            _singleInstanceActivationEvent.Set();
            return false;
        }

        _ownsSingleInstanceMutex = true;
        _singleInstanceActivationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _singleInstanceActivationEvent,
            (_, _) => _ = Dispatcher.BeginInvoke(ActivateMainWindow),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        return true;
    }

    private void ActivateMainWindow()
    {
        if (MainWindow is not { } window)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    private async Task LoadRemoteMenusAsync(CancellationToken cancellationToken)
    {
        var menuRegistry = Container.Resolve<IMenuRegistry>();
        var menuCache = Container.Resolve<IRemoteMenuConfigurationCache>();
        var logger = GetService<IClientLogger>();

        try
        {
            var cachedMenus = await menuCache.ReadAsync(cancellationToken);
            if (cachedMenus.Count > 0)
            {
                await Dispatcher.InvokeAsync(() => menuRegistry.ReplaceRemoteRange(cachedMenus));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger?.Warning(exception, "The cached menu configuration could not be loaded.");
        }

        IReadOnlyList<MenuItemModel> remoteMenus;
        try
        {
            var remoteMenuClient = Container.Resolve<IRemoteMenuConfigurationClient>();
            remoteMenus = await remoteMenuClient.GetMenusAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger?.Warning(exception, "Remote menu configuration is unavailable.");
            return;
        }

        try
        {
            await Dispatcher.InvokeAsync(() => menuRegistry.ReplaceRemoteRange(remoteMenus));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger?.Warning(exception, "The remote menu configuration could not be applied.");
            return;
        }

        try
        {
            await menuCache.WriteAsync(remoteMenus, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Application shutdown does not require the cache write to finish.
        }
        catch (Exception exception)
        {
            logger?.Warning(exception, "The remote menu configuration could not be cached.");
        }
    }
}
