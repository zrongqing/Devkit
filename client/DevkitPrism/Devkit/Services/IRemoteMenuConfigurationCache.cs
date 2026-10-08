using Devkit.Core.UI.Models;

namespace Devkit.Services;

public interface IRemoteMenuConfigurationCache
{
    Task<IReadOnlyList<MenuItemModel>> ReadAsync(CancellationToken cancellationToken = default);

    Task WriteAsync(
        IReadOnlyCollection<MenuItemModel> menus,
        CancellationToken cancellationToken = default);
}
