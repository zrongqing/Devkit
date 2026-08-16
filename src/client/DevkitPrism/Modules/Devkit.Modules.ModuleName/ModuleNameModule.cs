using Devkit.Core.UI.Services;
using Devkit.Modules.ModuleName.ViewModels;
using Devkit.Modules.ModuleName.Views;
using Devkit.Prism.Modules;

namespace Devkit.Modules.ModuleName;

public class ModuleNameModule : IModule, IUnloadableModule
{
    private const string ModuleId = "Devkit.Modules.ModuleName";

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var menuRegistry = containerProvider.Resolve<IMenuRegistry>();
        menuRegistry.ScanFromAssembly(GetType().Assembly, ModuleId);
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<ViewAViewModel>();
        containerRegistry.RegisterForNavigation<ViewA>();
    }

    public void OnUnloading(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IMenuRegistry>().UnregisterByModule(ModuleId);
    }
}
