using Devkit.Core.UI.Models;

namespace Devkit.Core.UI.Services;

public interface IShellService
{
    /// <summary>
    /// 获取菜单数据（未来可从数据库、DLL等获取）
    /// </summary>
    IEnumerable<MenuItemModel> LoadMenus();
    /// <summary>
    /// 获取所有的菜单
    /// </summary>
    /// <returns></returns>
    IEnumerable<MenuItemModel> LoadAllMenus();
    MenuItemModel? FindMenu(string id);

    /// <summary>
    /// 根据菜单项解析出对应的 View 实例
    /// </summary>
    object? ResolveContent(MenuItemModel menu);
}
