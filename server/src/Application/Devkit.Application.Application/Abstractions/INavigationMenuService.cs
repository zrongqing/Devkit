using Devkit.Application.Application.Contracts.Navigation;

namespace Devkit.Application.Application.Abstractions;

public interface INavigationMenuService
{
    IReadOnlyList<NavigationMenuItem> GetMenus(NavigationAudience audience);
}
