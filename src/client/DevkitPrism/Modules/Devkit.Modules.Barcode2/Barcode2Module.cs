using Devkit.Core.UI.Models;
using Devkit.Core.UI.Services;
using Devkit.Prism.Modules;
using Barcode2.Core.ApiCodeCollector;
using Barcode2.Configuration;
using Barcode2.Servers;
using Barcode2.ViewModels;
using Barcode2.Views;

namespace Barcode2;

public class Barcode2Module : IModule, IUnloadableModule
{
    private const string ModuleId = "Devkit.Modules.Barcode2";

    #region IModule Members
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var menuRegistry = containerProvider.Resolve<IMenuRegistry>();
        menuRegistry.Register(new MenuItemModel
        {
            Id = "barcode2",
            ModuleId = ModuleId,
            ParentId = null,
            Title = "barcode2",
            Order = 100
        });
        menuRegistry.ScanFromAssembly(GetType().Assembly, ModuleId);
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<ApiUpdateViewModel>();
        containerRegistry.Register<MenuTreeViewModel>();
        containerRegistry.Register<WebappUpdateViewModel>();
        containerRegistry.Register<Barcode2SettingsViewModel>();
        containerRegistry.RegisterForNavigation<ApiUpdateView>();
        containerRegistry.RegisterForNavigation<MenuTreeView>();
        containerRegistry.RegisterForNavigation<WebappUpdateView>();
        containerRegistry.RegisterForNavigation<Barcode2SettingsView>();
        containerRegistry.RegisterSingleton<IBarcode2ConfigurationService, Barcode2ConfigurationService>();
        containerRegistry.Register<IBarcode2ConnectionTester, Barcode2ConnectionTester>();
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
