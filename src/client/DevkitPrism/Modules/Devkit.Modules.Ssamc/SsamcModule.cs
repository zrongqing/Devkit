using Devkit.Core.UI.Services;
using Devkit.Modules.Ssamc.Core.ApiCodeCollector;
using Devkit.Modules.Ssamc.Servers;
using Module.Ssamc.Views;

namespace Devkit.Modules.Ssamc;

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
        containerRegistry.RegisterForNavigation<WebappUpdateView>();
        containerRegistry.RegisterSingleton<IApiScanner, RoslynApiScanner>();
        containerRegistry.Register<IApiUpdateServer, ApiUpdateServer>();
        containerRegistry.Register<WebappUpdateServer>();
    }
}
