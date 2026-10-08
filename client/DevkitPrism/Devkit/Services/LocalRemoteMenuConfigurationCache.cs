using System.IO;
using System.Text.Json;
using Devkit.Core.UI.Models;
using Devkit.Services.Interfaces.Configuration;

namespace Devkit.Services;

public sealed class LocalRemoteMenuConfigurationCache(ILocalSettingsStore settingsStore)
    : IRemoteMenuConfigurationCache
{
    private const string CacheScope = "client.navigation.menus";
    private const string CacheKey = "snapshot.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<MenuItemModel>> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsStore.ReadScopeAsync(CacheScope, cancellationToken);
        if (!settings.TryGetValue(CacheKey, out var json) || string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var cachedMenus = JsonSerializer.Deserialize<List<CachedMenuItem>>(json, JsonOptions)
                          ?? throw new InvalidDataException("本地菜单缓存内容无效。");

        return cachedMenus
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .Select(item => item.ToMenuItemModel())
            .ToList();
    }

    public Task WriteAsync(
        IReadOnlyCollection<MenuItemModel> menus,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(menus);

        var json = JsonSerializer.Serialize(
            menus.Select(CachedMenuItem.FromMenuItemModel),
            JsonOptions);
        return settingsStore.WriteScopeAsync(
            CacheScope,
            [new LocalSettingValue(CacheKey, json)],
            cancellationToken);
    }

    private sealed record CachedMenuItem(
        string Id,
        string? ParentId,
        string Title,
        int Order,
        bool IsVisible,
        string? ViewName,
        string? IconPath,
        bool AllowMultipleTabs,
        bool IsClosable)
    {
        public static CachedMenuItem FromMenuItemModel(MenuItemModel item) => new(
            item.Id,
            item.ParentId,
            item.Title,
            item.Order,
            item.IsVisible,
            item.ViewName,
            item.IconPath,
            item.AllowMultipleTabs,
            item.IsClosable);

        public MenuItemModel ToMenuItemModel() => new()
        {
            Id = Id,
            ParentId = ParentId,
            Title = Title,
            Order = Order,
            IsVisible = IsVisible,
            ViewName = ViewName,
            IconPath = IconPath,
            AllowMultipleTabs = AllowMultipleTabs,
            IsClosable = IsClosable
        };
    }
}
