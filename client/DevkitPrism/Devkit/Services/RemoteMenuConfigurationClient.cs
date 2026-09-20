using System.Net.Http;
using System.Net.Http.Json;
using Devkit.Contracts;
using Devkit.Core.UI.Models;

namespace Devkit.Services;

public class RemoteMenuConfigurationClient(HttpClient httpClient) : IRemoteMenuConfigurationClient
{
    private const string MenuConfigurationUrlVariable = "DEVKIT_MENU_CONFIG_URL";
    private const string DefaultMenuConfigurationUrl = "api/v1/client/navigation/menus";

    public async Task<IReadOnlyList<MenuItemModel>> GetMenusAsync(CancellationToken cancellationToken = default)
    {
        var menuConfigurationUrl = Environment.GetEnvironmentVariable(MenuConfigurationUrlVariable);
        if (string.IsNullOrWhiteSpace(menuConfigurationUrl))
        {
            menuConfigurationUrl = DefaultMenuConfigurationUrl;
        }

        var response = await httpClient.GetFromJsonAsync<ApiResponse<List<ClientNavigationMenuItemDto>>>(
            menuConfigurationUrl,
            cancellationToken);
        return response?.Data
                   .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                   .Select(x => new MenuItemModel
                   {
                       Id = x.Id,
                       ParentId = x.ParentId,
                       Title = string.IsNullOrWhiteSpace(x.Title) ? x.Id : x.Title,
                       Order = x.Order,
                       IsVisible = true,
                       ViewName = ClientNavigationViewRegistry.Resolve(x.ViewKey),
                       IsClosable = x.IsClosable,
                       AllowMultipleTabs = false
                   })
                   .ToList() ?? [];
    }
}
