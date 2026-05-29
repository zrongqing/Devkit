using System.Windows;
using Devkit.Core;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Services;
using Devkit.Modules.ModuleName;
using Devkit.Prism;
using Devkit.Services;
using Devkit.Services.Interfaces;
using Devkit.ViewModels;
using Devkit.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Syncfusion.Licensing;

namespace Devkit;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : DevkitPrismApplication
{
    private IHost _host;

    public App()
    {
        // Add your Syncfusion license key for WPF platform with corresponding Syncfusion NuGet version referred in project. For more information about license key see https://help.syncfusion.com/common/essential-studio/licensing/license-key.
        // Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Add your license key here"); 
        var licenseKey = Environment.GetEnvironmentVariable("SYNFUSION_LICENSE_KEY");
        SyncfusionLicenseProvider.RegisterLicense(licenseKey);
    }

    public T GetService<T>()
        where T : class
    {
        return _containerProvider.Resolve<T>() as T;
        // return _host.Services.GetService(typeof(T)) as T;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // For more information about .NET generic host see  https://docs.microsoft.com/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-3.0
        // var appLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        // var builder = Host.CreateDefaultBuilder(e.Args)
        //     .ConfigureAppConfiguration(c =>
        //     {
        //         c.SetBasePath(appLocation);
        //     })
        //     .ConfigureServices(ConfigureServices);
        // _host = builder.Build();

        ConfigureServices(GetPrismServiceCollection());
        base.OnStartup(e);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var apiBaseUrl = Environment.GetEnvironmentVariable("DEVKIT_API_BASE_URL") ?? "http://localhost:5000/";
        services.AddSingleton(new HttpClient { BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute) });
        services.AddSingleton<ISystemInfoClient, SystemInfoClient>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IMessageService, MessageService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IMenuRegistry, MenuRegistry>();
    }

    protected override Window CreateShell()
    {
        var reginon = Container.Resolve<IRegionManager>();
        reginon.RegisterViewWithRegion(RegionNames.MenuRegion, typeof(MenuView));
        var shellWin = Container.Resolve<ShellWindow>();

        LoadMenus();
        
        return shellWin;
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IShellService, ShellService>();
        containerRegistry.RegisterForNavigation<MenuView, MenuViewModel>(SysViewKeys.Menu);
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<ModuleNameModule>();
    }

    private void LoadMenus()
    {
        var menuRegistry = Container.Resolve<IMenuRegistry>();
        // ① 自动扫描本程序集中的 [MenuItem] 特性
        menuRegistry.ScanFromAssembly(GetType().Assembly);
        
        menuRegistry.Register(new MenuItemModel()
        {
            Id = "sys.user",
            ParentId = null,
            Title = "用户菜单",
            Order = 0,
        });
        
        // ② 代码动态注册（带导航参数）
        menuRegistry.Register(new MenuItemModel
        {
            Id = "sys.user.detail",
            ParentId = "sys.user",
            Title = "用户详情",
            Order = 20,
            ViewName = "UserView",
            Parameters = new NavigationParameters { { "mode", "detail" } }
        });

        // ③ VM-first 方式
        menuRegistry.Register(new MenuItemModel
        {
            Id = "sys.user.dashboard",
            ParentId = "sys.user",
            Title = "用户仪表盘",
            Order = 30,
            // ViewModelType = typeof(UserDashboardViewModel)
        });
    }
}
