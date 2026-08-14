using Devkit.Core.UI.Attributes;
using Devkit.Core.UI.Views;
using Ssamc.Models;
using Ssamc.ViewModels;
using Syncfusion.UI.Xaml.TreeView;

namespace Ssamc.Views;

[MenuItem(Id = "modules.ssamc.menutree", ParentId = "ssamc", Title = "menutree", Order = 22, ViewName = nameof(MenuTreeView))]
public partial class MenuTreeView : LoadingView
{
    public MenuTreeView()
    {
        InitializeComponent();
    }

    private async void MenuTree_OnItemDoubleTapped(
        object sender,
        ItemDoubleTappedEventArgs eventArgs)
    {
        if (DataContext is not MenuTreeViewModel viewModel ||
            eventArgs.Node?.Content is not MenuTreeNode node ||
            !viewModel.OpenCommand.CanExecute(node))
        {
            return;
        }

        await viewModel.OpenCommand.ExecuteAsync(node);
    }
}
