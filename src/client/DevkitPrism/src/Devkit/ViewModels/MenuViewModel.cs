using System.Collections.ObjectModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Mvvm;
using Devkit.Core.UI.Views;
using Devkit.Models;

namespace Devkit.ViewModels;

public partial class MenuViewModel : ViewModelBase
{
    private readonly IRegionManager _regionManager;
    private readonly IShellService _shellService;

    [ObservableProperty]
    private MenuItemModel _activeMenuItemModel = null;
    [ObservableProperty]
    private ListCollectionView _collectionView;

    [ObservableProperty]
    private ObservableCollection<MenuItemModel> _menus = new();

    public MenuViewModel()
    {
        // IShellService shellService
        _shellService = null;
    }

    [RelayCommand]
    private void Loaded()
    {
        // _shellService.ReloadMenus();
        // var menus = _shellService.GetMenus();
        // var convertedMenus = menus.Adapt<List<MenuItemModelModel>>();
        // var treeMenus = BuildHierarchy(convertedMenus);
        //
        // this.CollectionView = new ListCollectionView(treeMenus);
    }

    // partial void OnActiveMenuChanged(UIMenu value)
    // {
    //     if (value == null)
    //     {
    //         return;
    //     }
    //     var menuCode = value.Code;
    //     _ea.GetEvent<MenuClickEvent>().Publish(menuCode);
    // }

    /// <summary>
    /// 将扁平的 MenuModel 列表（带 Code / ParentCode）转换为树形结构。
    /// 返回根节点集合，子节点会被加入各自的 Items 集合。
    /// </summary>
    private static ObservableCollection<MenuItemModel> BuildHierarchy(IEnumerable<MenuItemModel> flatList)
    {
        if (flatList == null)
            return new ObservableCollection<MenuItemModel>();
        
        // 用 Code 建立字典方便查找
        var dict = new Dictionary<string, MenuItemModel>();
        var all = flatList.ToList();
        
        // 重置 Items，避免旧数据残留
        foreach (var item in all)
        {
            // 保证 Items 不为 null 并清空旧值
            item.Children = new ObservableCollection<MenuItemModel>();
            if (!string.IsNullOrEmpty(item.Id))
            {
                if (!dict.ContainsKey(item.Id))
                    dict[item.Id] = item;
            }
        }
        
        var roots = new List<MenuItemModel>();
        
        foreach (var item in all)
        {
            // 如果 ParentCode 为空或找不到父节点，则认为是根节点
            if (string.IsNullOrWhiteSpace(item.ParentId) || !dict.TryGetValue(item.ParentId, out var parent) || parent == item)
            {
                roots.Add(item);
            }
            else
            {
                // 防止循环引用：如果父节点是当前节点或已在子链中则跳过
                parent.Children.Add(item);
            }
        }

        return new ObservableCollection<MenuItemModel>(null);
    }

    #region Filtering
    internal delegate void FilterChanged();
    internal FilterChanged filterChanged;

    private string filterText = string.Empty;

    public string FilterText
    {
        get =>
            filterText;
        set
        {
            filterText = value;
            if (filterChanged != null)
                filterChanged();

            OnPropertyChanging(nameof(FilterText));
        }
    }
    #endregion
}
