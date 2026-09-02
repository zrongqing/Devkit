using System.Windows.Threading;
using Devkit.Core.UI.Attributes;
using Devkit.Core.UI.Views;
using Barcode2.Models;
using Barcode2.ViewModels;
using Syncfusion.UI.Xaml.TreeView;

namespace Barcode2.Views;

[MenuItem(Id = "barcode2.menusearch", ParentId = "barcode2", Title = "菜单检索", Order = 22, ViewName = nameof(MenuTreeView))]
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
