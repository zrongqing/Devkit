using System.IO;
using System.Text.Json;
using Barcode2.Configuration;
using Barcode2.Models;
using Devkit.Services.Interfaces.Configuration;

namespace Barcode2.Servers;

public sealed class LocalMenuTreeCache(ILocalSettingsStore settingsStore) : IMenuTreeCache
{
    private const string CacheScopePrefix = "barcode2.menu-tree";
    private const string CacheKey = "snapshot.v2";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<MenuTreeItem>> ReadAsync(
        string environmentKey,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsStore.ReadScopeAsync(
            GetScope(environmentKey),
            cancellationToken).ConfigureAwait(false);
        if (!settings.TryGetValue(CacheKey, out var json) || string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var snapshot = JsonSerializer.Deserialize<MenuTreeCacheSnapshot>(json, JsonOptions)
                       ?? throw new InvalidDataException("本地菜单检索缓存内容无效。");
        if (snapshot.Items.Count == 0 ||
            DateTimeOffset.UtcNow - snapshot.CachedAtUtc >= CacheLifetime)
        {
            return [];
        }

        return snapshot.Items;
    }

    public Task WriteAsync(
        string environmentKey,
        IReadOnlyCollection<MenuTreeItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        var snapshot = new MenuTreeCacheSnapshot(DateTimeOffset.UtcNow, items.ToArray());
        return settingsStore.WriteScopeAsync(
            GetScope(environmentKey),
            [new LocalSettingValue(CacheKey, JsonSerializer.Serialize(snapshot, JsonOptions))],
            cancellationToken);
    }

    private static string GetScope(string environmentKey) =>
        $"{CacheScopePrefix}.{Barcode2Defaults.ResolveEnvironmentKey(environmentKey)}";

    private sealed record MenuTreeCacheSnapshot(
        DateTimeOffset CachedAtUtc,
        IReadOnlyList<MenuTreeItem> Items);
}
