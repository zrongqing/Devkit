using System.Reflection;
using Devkit.Core.UI.Attributes;
using Devkit.Core.UI.Models;

namespace Devkit.Core.UI.Services;

/// <summary>
/// 菜单注册
/// </summary>
public interface IMenuRegistry
{
    event EventHandler? Changed;
    void Register(MenuItemModel item);
    void RegisterRemote(MenuItemModel item);
    void RegisterRange(IEnumerable<MenuItemModel> items);
    void RegisterRemoteRange(IEnumerable<MenuItemModel> items);
    void ScanFromAssembly(Assembly assembly, string? moduleId = null);
    IReadOnlyList<MenuItemModel> GetFlatMenus();
    MenuItemModel? Find(string id);
    void UnregisterByModule(string moduleId);
}

public class MenuRegistry : IMenuRegistry
{
    private readonly Dictionary<string, MenuItemModel> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _remoteMenuIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly IContainerProvider _container;

    public MenuRegistry(IContainerProvider container) => _container = container;

    public event EventHandler? Changed;

    public void Register(MenuItemModel item) => RegisterCore(item, isRemote: false);

    public void RegisterRemote(MenuItemModel item) => RegisterCore(item, isRemote: true);

    private void RegisterCore(MenuItemModel item, bool isRemote)
    {
        if (string.IsNullOrEmpty(item.Id))
            throw new InvalidOperationException("菜单项必须指定 Id");

        if (!isRemote && _remoteMenuIds.Contains(item.Id))
        {
            return;
        }

        _items[item.Id] = item;

        if (isRemote)
        {
            _remoteMenuIds.Add(item.Id);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RegisterRange(IEnumerable<MenuItemModel> items)
    {
        foreach (var it in items) Register(it);
    }

    public void RegisterRemoteRange(IEnumerable<MenuItemModel> items)
    {
        foreach (var it in items) RegisterRemote(it);
    }

    /// <summary>扫描程序集中带 [MenuItem] 特性的 View 类型</summary>
    public void ScanFromAssembly(Assembly assembly, string? moduleId = null)
    {
        foreach (var type in assembly.GetTypes())
        {
            var attrs = type.GetCustomAttributes<MenuItemAttribute>();
            foreach (var a in attrs)
            {
                Register(new MenuItemModel
                {
                    Id          = a.Id,
                    ModuleId    = moduleId,
                    Title       = a.Title,
                    IconPath        = a.IconPath,
                    ParentId    = a.ParentId,
                    Order       = a.Order,
                    ViewName    = string.IsNullOrEmpty(a.ViewName) ? type.Name : a.ViewName
                });
            }
        }
    }

    public void UnregisterByModule(string moduleId)
    {
        var ids = _items
            .Where(pair => string.Equals(pair.Value.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var id in ids)
        {
            _items.Remove(id);
            _remoteMenuIds.Remove(id);
        }

        if (ids.Length > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<MenuItemModel> GetFlatMenus() => _items.Values.ToList();

    public MenuItemModel? Find(string id)
    {
        return _items.TryGetValue(id, out var item) ? item : null;
    }
}
