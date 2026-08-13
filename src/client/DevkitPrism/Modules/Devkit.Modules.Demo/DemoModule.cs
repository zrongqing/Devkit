using Devkit.Core.UI.Models;
using Devkit.Core.UI.Services;
using Devkit.Modules.Demo.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace Devkit.Modules.Demo;

public sealed class DemoModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var menuRegistry = containerProvider.Resolve<IMenuRegistry>();
        menuRegistry.Register(new MenuItemModel
        {
            Id = "demo",
            ParentId = null,
            Title = "demo",
            Order = 110
        });
        menuRegistry.ScanFromAssembly(GetType().Assembly);
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<LoadingDemoView>();
    }
}
