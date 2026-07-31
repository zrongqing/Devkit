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

public class ShellService : IShellService
{
    private readonly IMenuRegistry _registry;
    private readonly IContainerProvider _container;
    private readonly IRegionManager _regionManager;
    private readonly IModuleCatalog _moduleCatalog;

    public ShellService(
        IMenuRegistry registry,
        IContainerProvider container, 
        IRegionManager regionManager, 
        IModuleCatalog moduleCatalog)
    {
        _registry = registry;
        _container = container;
        _regionManager = regionManager;
        _moduleCatalog = moduleCatalog;
    }

    /// <summary>组装成树形结构返回</summary>
    public IEnumerable<MenuItemModel> LoadMenus()
    {
        var flat = _registry.GetFlatMenus()
                            .Where(x => x.IsVisible)
                            .OrderBy(x => x.Order)
                            .ToList();

        var lookup = flat.ToLookup(x => x.ParentId ?? string.Empty);
        var roots = lookup[string.Empty].OrderBy(x => x.Order).ToList();

        foreach (var root in roots)
            BuildTree(root, lookup);

        return roots;
    }

    public IEnumerable<MenuItemModel> LoadAllMenus()
    {
        return _registry.GetFlatMenus().OrderBy(x => x.Order).ToList();
    }

    public MenuItemModel? FindMenu(string id) => _registry.Find(id);

    private static void BuildTree(MenuItemModel node, ILookup<string, MenuItemModel> lookup)
    {
        node.Children.Clear();
        foreach (var child in lookup[node.Id].OrderBy(x => x.Order))
        {
            node.Children.Add(child);
            BuildTree(child, lookup);
        }
    }

    /// <summary>根据菜单解析 ViewModel 实例</summary>
    public object? ResolveContent(MenuItemModel menu)
    {
        if (menu == null) return null;

        // 方式 1：View-first，解析 Prism 导航注册的 View。
        if (!string.IsNullOrEmpty(menu.ViewName))
        {
            return _container.Resolve<object>(menu.ViewName);
        }

        // 方式 2：ViewModel-first。
        if (menu.ViewModelType != null)
        {
            return _container.Resolve(menu.ViewModelType);
        }

        return null;
    }
}
