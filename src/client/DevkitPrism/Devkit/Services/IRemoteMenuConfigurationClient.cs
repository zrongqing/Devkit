using Devkit.Core.UI.Models;

namespace Devkit.Services;

public interface IRemoteMenuConfigurationClient
{
    Task<IReadOnlyList<MenuItemModel>> GetMenusAsync(CancellationToken cancellationToken = default);
}
