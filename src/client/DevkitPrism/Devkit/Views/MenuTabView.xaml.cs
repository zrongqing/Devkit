using System.Windows.Controls;

using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Devkit.Views;

public partial class MenuTabView : UserControl
{
    private bool _tabDragSuspended;

    public MenuTabView()
    {
        InitializeComponent();
    }

    private void OnTabControlPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || FindVisualAncestor<ButtonBase>(source) == null)
        {
            return;
        }

        // Pinning or closing moves/removes the item container. If Syncfusion also starts a
        // drag from that button, it can later try to insert DependencyProperty.UnsetValue.
        _tabDragSuspended = true;
        MenuTabs.AllowDragDrop = false;
    }

    private void OnTabControlPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_tabDragSuspended)
        {
            return;
        }

        _tabDragSuspended = false;
        MenuTabs.AllowDragDrop = true;
    }

    private static T? FindVisualAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T ancestor)
            {
                return ancestor;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}

