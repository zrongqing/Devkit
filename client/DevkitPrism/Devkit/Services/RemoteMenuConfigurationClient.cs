using System.Net.Http;
using System.Net.Http.Json;
using Devkit.Core.UI.Models;

namespace Devkit.Services;

public class RemoteMenuConfigurationClient(HttpClient httpClient) : IRemoteMenuConfigurationClient
{
    private const string MenuConfigurationUrlVariable = "DEVKIT_MENU_CONFIG_URL";

    public async Task<IReadOnlyList<MenuItemModel>> GetMenusAsync(CancellationToken cancellationToken = default)
    {
        var menuConfigurationUrl = Environment.GetEnvironmentVariable(MenuConfigurationUrlVariable);
        if (string.IsNullOrWhiteSpace(menuConfigurationUrl))
        {
            return [];
        }

        var remoteMenus = await httpClient.GetFromJsonAsync<List<RemoteMenuItemDto>>(menuConfigurationUrl, cancellationToken);
        return remoteMenus?
                   .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                   .Select(x => new MenuItemModel
                   {
                       Id = x.Id,
                       ParentId = x.ParentId,
                       Title = string.IsNullOrWhiteSpace(x.Title) ? x.Id : x.Title,
                       Order = x.Order,
                       IsVisible = x.IsVisible,
                       ViewName = x.ViewName,
                       IconPath = x.IconPath,
                       IsClosable = x.IsClosable,
                       AllowMultipleTabs = x.AllowMultipleTabs,
                       Parameters = ToNavigationParameters(x.Parameters)
                   })
                   .ToList() ?? [];
    }

    private static NavigationParameters? ToNavigationParameters(Dictionary<string, string>? parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return null;
        }

        var navigationParameters = new NavigationParameters();
        foreach (var parameter in parameters)
        {
            navigationParameters.Add(parameter.Key, parameter.Value);
        }

        return navigationParameters;
    }

    private sealed class RemoteMenuItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string? ParentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsVisible { get; set; } = true;
        public string? ViewName { get; set; }
        public string? IconPath { get; set; }
        public bool IsClosable { get; set; } = true;
        public bool AllowMultipleTabs { get; set; }
        public Dictionary<string, string>? Parameters { get; set; }
    }
}
