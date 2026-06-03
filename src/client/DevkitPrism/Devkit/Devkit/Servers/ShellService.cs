using Devkit.Core.Contracts.Views;
using Devkit.Core.UI.Models;

namespace Devkit.Servers;

public class ShellService : IShellService
{
    private readonly IContainerProvider _container;
    
    public ShellService(IContainerProvider container)
    {
        _container = container;
    }
    
    public IEnumerable<MenuItemModel> LoadMenus()
    {
        throw new NotImplementedException();
    }
    public object ResolveContent(MenuItemModel menu)
    {
        throw new NotImplementedException();
    }
}
