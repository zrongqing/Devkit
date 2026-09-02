using Barcode2.Models;

namespace Barcode2.Servers;

/// <summary>
/// Supplies the menu hierarchy. Replace the default registration when a
/// remote or persisted source becomes available.
/// </summary>
public interface IMenuTreeDataSource
{
    Task<IReadOnlyList<MenuTreeItem>> GetMenuTreeAsync(
        string environmentKey,
        CancellationToken cancellationToken);
}
