using Devkit.Core.UI.Models;
using Devkit.Core.UI.Services;
using Devkit.Modules.ModuleManagement.ViewModels;
using Devkit.Modules.ModuleManagement.Views;

namespace Devkit.Modules.ModuleManagement;

public sealed class ModuleManagementModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<ModuleManagementViewModel>();
        containerRegistry.RegisterForNavigation<ModuleManagementView>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IMenuRegistry>().Register(new MenuItemModel
        {
            Id = "modules.management",
            ParentId = "modules",
            Title = "模块管理",
            Order = -100,
            ViewName = nameof(ModuleManagementView),
            AllowMultipleTabs = false
        });

        containerProvider.Resolve<DynamicModuleManager>().Initialize();
    }
}
