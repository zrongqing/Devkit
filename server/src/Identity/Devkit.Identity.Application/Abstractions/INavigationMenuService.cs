using Devkit.Identity.Application.Contracts.Navigation;

namespace Devkit.Identity.Application.Abstractions;

public interface INavigationMenuService
{
    IReadOnlyList<NavigationMenuItem> GetMenus(NavigationAudience audience);
}
