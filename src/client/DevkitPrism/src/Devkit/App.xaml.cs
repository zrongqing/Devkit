using System.Windows;
using Devkit.Core;
using Devkit.Core.UI.Views;
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
        services.AddSingleton<IFileService, FileService>();
    }

    protected override Window CreateShell()
    {
        var reginon = Container.Resolve<IRegionManager>();
        reginon.RegisterViewWithRegion(RegionNames.MenuRegion, typeof(MenuView));
        
        var shellWin = Container.Resolve<ShellWindow>();
        try
        {
            var aa = Container.Resolve<IFileService>();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return shellWin;
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IMessageService, MessageService>();
        
        containerRegistry.RegisterForNavigation<MenuView, MenuViewModel>(SysViewKeys.Menu);
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<ModuleNameModule>();
    }
    
}
