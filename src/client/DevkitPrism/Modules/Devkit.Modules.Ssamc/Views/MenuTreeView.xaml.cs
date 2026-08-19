using System.Windows.Threading;
using Devkit.Core.UI.Attributes;
using Devkit.Core.UI.Views;
using Ssamc.Models;
using Ssamc.ViewModels;
using Syncfusion.UI.Xaml.TreeView;

namespace Ssamc.Views;

[MenuItem(Id = "ssamc.menusearch", ParentId = "ssamc", Title = "菜单检索", Order = 22, ViewName = nameof(MenuTreeView))]
public partial class MenuTreeView : LoadingView
{
    public MenuTreeView(MenuTreeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void MenuTree_OnSelectionChanged(
        object sender,
        ItemSelectionChangedEventArgs eventArgs)
    {
        if (DataContext is not MenuTreeViewModel viewModel)
        {
            return;
        }

        viewModel.SelectedMenu = eventArgs.AddedItems
            .OfType<MenuTreeNode>()
            .FirstOrDefault();

        _ = MenuDetailsPropertyGrid.Dispatcher.BeginInvoke(
            DispatcherPriority.DataBind,
            new Action(MenuDetailsPropertyGrid.RefreshPropertygrid));
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
