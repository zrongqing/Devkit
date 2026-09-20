using Devkit.Server.Application.Contracts.Navigation;

namespace Devkit.Server.Application.Abstractions;

public interface INavigationMenuService
{
    IReadOnlyList<NavigationMenuItem> GetMenus(NavigationAudience audience);
}
