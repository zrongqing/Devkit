using Devkit.Application.Application.Abstractions;
using Devkit.Application.Application.Contracts.Navigation;
using Devkit.Application.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Devkit.Application.Infrastructure.Navigation;

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
