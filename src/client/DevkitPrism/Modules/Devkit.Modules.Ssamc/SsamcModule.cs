using Devkit.Core.UI.Services;
using Module.Ssamc.Servers;
using Module.Ssamc.Views;
using Ssamc.Core.ApiCodeCollector;

namespace Module.Ssamc;

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
        containerRegistry.Register<ApiUpdateServer>();
        containerRegistry.Register<WebappUpdateServer>();
    }
}
