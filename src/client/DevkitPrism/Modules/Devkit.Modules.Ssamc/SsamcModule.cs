using Devkit.Core.UI.Services;
using Ssamc.Core.ApiCodeCollector;
using Ssamc.Servers;
using Ssamc.Views;

namespace Ssamc;

public class SsamcModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var menuRegistry = containerProvider.Resolve<IMenuRegistry>();
        menuRegistry.Register(new()
        {
            Id = "ssamc",
            ParentId = null,
            Title = "ssamc",
            Order = 100,
        });
        menuRegistry.ScanFromAssembly(GetType().Assembly);
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<ApiUpdateView>();
        containerRegistry.RegisterForNavigation<MenuTreeView>();
        containerRegistry.RegisterForNavigation<WebappUpdateView>();
        containerRegistry.RegisterSingleton<IApiScanner, RoslynApiScanner>();
        containerRegistry.Register<IApiUpdateServer, ApiUpdateServer>();
        containerRegistry.RegisterSingleton<IMenuTreeDataSource, OracleMenuTreeDataSource>();
        containerRegistry.RegisterSingleton<IWebPageLauncher, SystemWebPageLauncher>();
        containerRegistry.Register<WebappUpdateServer>();
    }
}
