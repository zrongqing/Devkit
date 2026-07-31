using Devkit.Core.UI.Services;
using Devkit.Modules.ModuleName.Views;

namespace Devkit.Modules.ModuleName;

public class ModuleNameModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var menuRegistry = containerProvider.Resolve<IMenuRegistry>();
        menuRegistry.ScanFromAssembly(GetType().Assembly);
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<ViewA>();
    }
}
