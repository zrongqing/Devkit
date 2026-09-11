using Devkit.Core.UI.Models;
using Devkit.Core.UI.Services;
using Devkit.Modules;

namespace Devkit.Services;

public sealed class ShellService(
    IMenuRegistry registry,
    IContainerProvider container,
    DynamicModuleManager moduleManager) : IShellService
{
    public IEnumerable<MenuItemModel> LoadMenus()
    {
        var flat = registry.GetFlatMenus()
            .Where(item => item.IsVisible)
            .OrderBy(item => item.Order)
            .ToList();
        var lookup = flat.ToLookup(item => item.ParentId ?? string.Empty);
        var roots = lookup[string.Empty].OrderBy(item => item.Order).ToList();

        foreach (var root in roots)
        {
            BuildTree(root, lookup);
        }

        return roots;
    }

    public IEnumerable<MenuItemModel> LoadAllMenus() =>
        registry.GetFlatMenus().OrderBy(item => item.Order).ToList();

    public MenuItemModel? FindMenu(string id) => registry.Find(id);

    public object? ResolveContent(MenuItemModel menu)
    {
        ArgumentNullException.ThrowIfNull(menu);

        if (!string.IsNullOrWhiteSpace(menu.ViewName))
        {
            return string.IsNullOrWhiteSpace(menu.ModuleId)
                ? container.Resolve<object>(menu.ViewName)
                : moduleManager.Resolve(menu.ModuleId, menu.ViewName);
        }

        if (menu.ViewModelType != null)
        {
            if (!string.IsNullOrWhiteSpace(menu.ModuleId))
            {
                throw new InvalidOperationException("动态模块菜单必须使用 ViewName，不能在宿主中保存模块 Type 引用。");
            }

            return container.Resolve(menu.ViewModelType);
        }

        return null;
    }

    private static void BuildTree(MenuItemModel node, ILookup<string, MenuItemModel> lookup)
    {
        node.Children.Clear();
        foreach (var child in lookup[node.Id].OrderBy(item => item.Order))
        {
            node.Children.Add(child);
            BuildTree(child, lookup);
        }
    }
}
