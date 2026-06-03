using System.Collections.ObjectModel;

namespace Devkit.Models;

public class MenuItemModelModel : Devkit.Core.UI.Models.MenuItemModel
{
    public ObservableCollection<MenuItemModelModel> Items { get; set; } = new();
}
