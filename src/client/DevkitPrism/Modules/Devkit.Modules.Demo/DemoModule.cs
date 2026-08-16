using Devkit.Core.UI.Models;
using Devkit.Core.UI.Services;
using Devkit.Modules.Demo.ViewModels;
using Devkit.Modules.Demo.Views;
using Devkit.Prism.Modules;
using Prism.Ioc;
using Prism.Modularity;

namespace Devkit.Modules.Demo;

public sealed class DemoModule : IModule, IUnloadableModule
{
    private const string ModuleId = "Devkit.Modules.Demo";

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var menuRegistry = containerProvider.Resolve<IMenuRegistry>();
        menuRegistry.Register(new MenuItemModel
        {
            Id = "demo",
            ModuleId = ModuleId,
            ParentId = "modules",
            Title = "demo",
            Order = 110
        });
        menuRegistry.ScanFromAssembly(GetType().Assembly, ModuleId);
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<LoadingDemoViewModel>();
        containerRegistry.RegisterForNavigation<LoadingDemoView>();
    }

    public void OnUnloading(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IMenuRegistry>().UnregisterByModule(ModuleId);
    }
}
