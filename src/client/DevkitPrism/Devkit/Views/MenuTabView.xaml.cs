using System.Windows.Controls;

using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Devkit.Core.UI.Models;
using Devkit.ViewModels;
using Syncfusion.Windows.Tools.Controls;

namespace Devkit.Views;

public partial class MenuTabView : UserControl
{
    private bool _tabDragSuspended;

    public MenuTabView()
    {
        InitializeComponent();
    }

    private void OnTabsClosing(object sender, CloseTabEventArgs e)
    {
        // TabControlExt hides closed tabs by default, leaving the item in Tabs. Route all
        // closes through the view model so the item, its load operation, and its content
        // are released together.
        e.Cancel = true;

        if (DataContext is not MenuTabViewModel viewModel)
        {
            return;
        }

        var tabs = e.ClosingTabItems?
            .Select(GetTabModel)
            .OfType<TabItemModel>()
            .Distinct()
            .ToArray() ?? [];

        if (tabs.Length == 0
            && (GetTabModel(e.TargetTabItem) ?? GetTabModel(MenuTabs.SelectedItem)) is { } targetTab)
        {
            tabs = [targetTab];
        }

        foreach (var tab in tabs)
        {
            viewModel.CloseTabCommand.Execute(tab);
        }
    }

    private void OnTabItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || FindAncestor<ButtonBase>(source) == null)
        {
            return;
        }

        // Pinning or closing moves/removes the item container. If Syncfusion also starts a
        // drag from that button, it can later try to insert DependencyProperty.UnsetValue.
        _tabDragSuspended = true;
        MenuTabs.AllowDragDrop = false;
    }

    private void OnTabItemPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_tabDragSuspended)
        {
            return;
        }

        _tabDragSuspended = false;
        MenuTabs.AllowDragDrop = true;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        DependencyObject? candidate = current;

        while (candidate != null)
        {
            if (candidate is T ancestor)
            {
                return ancestor;
            }

            candidate = GetParent(candidate);
        }

        return null;
    }

    private static TabItemModel? GetTabModel(object? item) => item switch
    {
        TabItemModel tab => tab,
        FrameworkElement { DataContext: TabItemModel tab } => tab,
        _ => null
    };

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is Visual or Visual3D)
        {
            return VisualTreeHelper.GetParent(current);
        }

        if (current is ContentElement contentElement)
        {
            return ContentOperations.GetParent(contentElement)
                ?? (contentElement as FrameworkContentElement)?.Parent;
        }

        return null;
    }
}

