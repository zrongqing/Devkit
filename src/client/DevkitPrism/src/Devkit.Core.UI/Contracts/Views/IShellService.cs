using Devkit.Core.UI.Models;

namespace Devkit.Core.UI.Views;

public interface IShellService
{
    /// <summary>
    /// 获取菜单数据（未来可从数据库、DLL等获取）
    /// </summary>
    IEnumerable<MenuItemModel> LoadMenus();

    /// <summary>
    /// 根据菜单项解析出对应的 ViewModel 实例
    /// </summary>
    object ResolveContent(MenuItemModel menu);

    // /// <summary>
    // /// 当前激活的标签页
    // /// </summary>
    // UITabItemModel ActiveUITabItemModel { get; set; }
    // /// <summary>
    // /// 当前激活的菜单
    // /// </summary>
    // UIMenuItem ActiveUIMenuItem { get; set; }
    //
    // public List<UIMenuItem> GetMenus();
    //
    // /// <summary>
    // /// 重新加载菜单
    // /// </summary>
    // public void ReloadMenus();
    // /// <summary>
    // /// 打开菜单
    // /// </summary>
    // public void OpenMenu(string code);
    // /// <summary>
    // /// 关闭菜单
    // /// </summary>
    // /// <param name="code"></param>
    // public void CloseMenu(string code);
    //
    // public void OpenMenu(UIMenuItem uiMenuItem);
    //
    // public void RegisterMenu(UIMenuItem uiMenuItem);
}
