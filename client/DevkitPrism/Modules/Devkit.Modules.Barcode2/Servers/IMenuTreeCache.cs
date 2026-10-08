using Barcode2.Models;

namespace Barcode2.Servers;

public interface IMenuTreeCache
{
    Task<IReadOnlyList<MenuTreeItem>> ReadAsync(
        string environmentKey,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        string environmentKey,
        IReadOnlyCollection<MenuTreeItem> items,
        CancellationToken cancellationToken = default);
}
