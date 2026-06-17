using System.Collections.ObjectModel;
using Devkit.Core.UI.Models;

namespace Devkit.Models;

public class MenuTree : MenuItemModel
{
    public ObservableCollection<MenuTree> Items { get; set; } = new();
}
