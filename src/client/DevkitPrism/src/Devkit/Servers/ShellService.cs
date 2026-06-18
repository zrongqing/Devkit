using Devkit.Core.UI.Models;
using Devkit.Core.UI.Views;
using Devkit.ViewModels;

namespace Devkit.Servers;

public class ShellService : IShellService
{
    private readonly IContainerProvider _containerProvider;

    public ShellService(IContainerProvider containerProvider)
    {
        _containerProvider = containerProvider;
    }

    #region IShellService Members
    public IEnumerable<MenuItemModel> LoadMenus()
    {
        return new List<MenuItemModel>
        {
            new()
                { Title = "首页", ViewModelType = typeof(HomeViewModel), IsClosable = false },
            new()
                { Title = "系统设置", ViewModelType = typeof(SettingViewModel) }
        };
    }
    public object ResolveContent(MenuItemModel menu)
    {
        if (menu?.ViewModelType == null)
            return null;

        // 利用 Prism 的 DI 容器动态解析 ViewModel
        // 这样 ViewModel 的构造函数中注入的服务也能被正常解析
        return _containerProvider.Resolve(menu.ViewModelType);
    }
    #endregion
}
