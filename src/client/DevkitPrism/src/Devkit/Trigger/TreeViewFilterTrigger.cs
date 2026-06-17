using System.Windows.Controls;
using Devkit.Models;
using Devkit.ViewModels;
using Microsoft.Xaml.Behaviors;
using Syncfusion.UI.Xaml.TreeView;

namespace Devkit.Core.UI.Trigger;

public class TreeViewFilterTrigger : TargetedTriggerAction<SfTreeView>
{
    protected override void Invoke(object parameter)
    {
        var viewModel = this.Target.DataContext as MenuViewModel;
        viewModel.filterChanged += OnFilterChanged;
    }
    private void OnFilterChanged()
    {
        var viewModel = this.Target.DataContext as MenuViewModel;
        viewModel.CollectionView?.Filter = (e) =>
        {
            if (e is MenuTree file)
            {
                return MatchesFilter(file, viewModel.FilterText);
            }
            return false;
        };

        this.Target.ExpandAll();

    }

    // 递归检查所有层级
    private bool MatchesFilter(MenuTree item, string filterText)
    {
        if (string.IsNullOrEmpty(filterText)) return true;

        // 检查当前项
        if (item.Title?.ToLower().Contains(filterText.ToLower()) == true)
            return true;

        var children = item.Items ?? item.Items;
        if (children != null)
        {
            foreach (var child in children)
            {
                if (MatchesFilter(child, filterText))
                    return true;
            }
        }

        return false;
    }
}


