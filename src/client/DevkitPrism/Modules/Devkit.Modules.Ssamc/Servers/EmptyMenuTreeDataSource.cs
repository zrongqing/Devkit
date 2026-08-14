using Ssamc.Models;

namespace Ssamc.Servers;

/// <summary>
/// Safe default used until the business menu source is connected.
/// </summary>
public sealed class EmptyMenuTreeDataSource : IMenuTreeDataSource
{
    public Task<IReadOnlyList<MenuTreeItem>> GetMenuTreeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<MenuTreeItem>>([]);
    }
}
