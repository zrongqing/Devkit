using Devkit.Core.UI.Models;
using Devkit.Core.UI.Servers;

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
}

public class ShellService : IShellService
{
    private readonly IMenuRegistry _registry;
    private readonly IContainerProvider _container;
    private readonly IRegionManager _regionManager;
    private readonly IModuleCatalog _moduleCatalog;

    public ShellService(
        IContainerProvider container, 
        IRegionManager regionManager, 
        IModuleCatalog moduleCatalog)
    {
        // _registry = registry;
        _container = container;
        _regionManager = regionManager;
        _moduleCatalog = moduleCatalog;
    }

    /// <summary>组装成树形结构返回</summary>
    public IEnumerable<MenuItemModel> LoadMenus()
    {
        var flat = _registry.GetFlatMenus()
                            .OrderBy(x => x.Order)
                            .ToList();

        var lookup = flat.ToLookup(x => x.ParentId);
        var roots = lookup[null].Concat(lookup[""]);

        foreach (var root in roots)
            BuildTree(root, lookup);

        return roots;
    }

    private void BuildTree(MenuItemModel node, ILookup<string, MenuItemModel> lookup)
    {
        node.Children.Clear();
        foreach (var child in lookup[node.Id])
        {
            node.Children.Add(child);
            BuildTree(child, lookup);
        }
    }

    /// <summary>根据菜单解析 ViewModel 实例</summary>
    public object ResolveContent(MenuItemModel menu)
    {
        if (menu == null) return null;

        // 方式 1：View-first，使用 Prism 区域导航
        if (!string.IsNullOrEmpty(menu.ViewName))
        {
            _regionManager.RequestNavigate("ContentRegion", menu.ViewName);
            // 同时返回已激活视图的 DataContext
            var view = _regionManager.Regions["ContentRegion"].ActiveViews.FirstOrDefault();
            return view?.GetType().GetProperty("DataContext")?.GetValue(view);
        }

        // 方式 2：ViewModel-first
        if (menu.ViewModelType != null)
        {
            return _container.Resolve(menu.ViewModelType);
        }

        return null;
    }
}