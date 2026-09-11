using Barcode2.Models;

namespace Barcode2.Servers;

/// <summary>
/// Safe default used until the business menu source is connected.
/// </summary>
public sealed class EmptyMenuTreeDataSource : IMenuTreeDataSource
{
    #region IMenuTreeDataSource Members
    public Task<IReadOnlyList<MenuTreeItem>> GetMenuTreeAsync(
        string environmentKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentKey);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<MenuTreeItem>>([]);
    }
    #endregion
}
