using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts.Navigation;
using Devkit.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Devkit.Server.Infrastructure.Navigation;

public sealed class NavigationMenuService : INavigationMenuService
{
    private readonly IReadOnlyList<NavigationMenuItem> _webMenus;
    private readonly IReadOnlyList<NavigationMenuItem> _clientMenus;

    public NavigationMenuService(IOptions<NavigationOptions> options)
    {
        _webMenus = NavigationMenuComposer.Compose(options.Value, NavigationAudience.Web);
        _clientMenus = NavigationMenuComposer.Compose(options.Value, NavigationAudience.Client);
    }

    public IReadOnlyList<NavigationMenuItem> GetMenus(NavigationAudience audience) =>
        audience == NavigationAudience.Web ? _webMenus : _clientMenus;
}
