using System.Reflection;
using Devkit.Core.UI.Attributes;
using Devkit.Core.UI.Models;

namespace Devkit.Core.UI.Servers;

public interface IMenuRegistry
{
    void                         Register(MenuItemModel item);
    void                         RegisterRange(IEnumerable<MenuItemModel> items);
    void                         ScanFromAssembly(Assembly assembly);
    IReadOnlyList<MenuItemModel> GetFlatMenus();
}

class MenuRegistry : IMenuRegistry
{
    private readonly List<MenuItemModel> _items = new();
    private readonly IContainerProvider _container;

    public MenuRegistry(IContainerProvider container) => _container = container;

    public void Register(MenuItemModel item)
    {
        if (string.IsNullOrEmpty(item.Id))
            throw new InvalidOperationException("菜单项必须指定 Id");
        _items.Add(item);
    }

    public void RegisterRange(IEnumerable<MenuItemModel> items)
    {
        foreach (var it in items) Register(it);
    }

    /// <summary>扫描程序集中带 [MenuItem] 特性的 View 类型</summary>
    public void ScanFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            var attrs = type.GetCustomAttributes<MenuItemAttribute>();
            foreach (var a in attrs)
            {
                Register(new MenuItemModel
                {
                    Id          = a.Id,
                    Title       = a.Title,
                    IconPath        = a.IconPath,
                    ParentId    = a.ParentId,
                    Order       = a.Order,
                    ViewName    = string.IsNullOrEmpty(a.ViewName) ? type.Name : a.ViewName,
                });
            }
        }
    }

    public IReadOnlyList<MenuItemModel> GetFlatMenus() => _items;
}