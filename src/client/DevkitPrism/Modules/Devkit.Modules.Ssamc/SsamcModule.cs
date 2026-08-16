using Devkit.Core.UI.Models;
using Devkit.Core.UI.Services;
using Devkit.Prism.Modules;
using Ssamc.Core.ApiCodeCollector;
using Ssamc.Servers;
using Ssamc.ViewModels;
using Ssamc.Views;

namespace Ssamc;

public class SsamcModule : IModule, IUnloadableModule
{
    private const string ModuleId = "Devkit.Modules.Ssamc";

    #region IModule Members
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var menuRegistry = containerProvider.Resolve<IMenuRegistry>();
        menuRegistry.Register(new MenuItemModel
        {
            Id = "ssamc",
            ModuleId = ModuleId,
            ParentId = null,
            Title = "ssamc",
            Order = 100
        });
        menuRegistry.ScanFromAssembly(GetType().Assembly, ModuleId);
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<ApiUpdateViewModel>();
        containerRegistry.Register<MenuTreeViewModel>();
        containerRegistry.Register<WebappUpdateViewModel>();
        containerRegistry.RegisterForNavigation<ApiUpdateView>();
        containerRegistry.RegisterForNavigation<MenuTreeView>();
        containerRegistry.RegisterForNavigation<WebappUpdateView>();
        containerRegistry.RegisterSingleton<IApiScanner, RoslynApiScanner>();
        containerRegistry.Register<IApiUpdateServer, ApiUpdateServer>();
        containerRegistry.RegisterSingleton<IMenuTreeDataSource, OracleMenuTreeDataSource>();
        containerRegistry.RegisterSingleton<IWebPageLauncher, SystemWebPageLauncher>();
        containerRegistry.Register<WebappUpdateServer>();
    }
    #endregion

    #region IUnloadableModule Members
    public void OnUnloading(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IMenuRegistry>().UnregisterByModule(ModuleId);
    }
    #endregion
}
