using System.Collections.ObjectModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.Contracts.Views;
using Devkit.Core.Mvvm;
using Devkit.Core.UI.Models;
using Devkit.Models;
using Devkit.Prism.Events;
using Mapster;

namespace Devkit.ViewModels;

public partial class MenuViewModel : ViewModelBase
{
    private readonly IEventAggregator _ea;
    private readonly IRegionManager _regionManager;
    private readonly IShellService _shellService;

    [ObservableProperty]
    private ObservableCollection<MenuItemModelModel> _menus = new ObservableCollection<MenuItemModelModel>();
    [ObservableProperty]
    private ListCollectionView _collectionView;

    [ObservableProperty]
    private MenuItemModel _activeMenuItemModel = null;

    public MenuViewModel(IEventAggregator ea, IShellService shellService)
    {
        _ea = ea;
        _shellService = shellService;
    }
    #region Filtering

    internal delegate void FilterChanged();
    internal FilterChanged filterChanged;

    private string filterText = string.Empty;

    public string FilterText
    {
        get { return filterText; }
        set
        {
            filterText = value;
            if (filterChanged != null)
                filterChanged();

            OnPropertyChanging(nameof(FilterText));
        }
    }

    #endregion

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
    private static ObservableCollection<MenuItemModelModel> BuildHierarchy(IEnumerable<MenuItemModelModel> flatList)
    {
        if (flatList == null)
            return new ObservableCollection<MenuItemModelModel>();

        // 用 Code 建立字典方便查找
        var dict = new Dictionary<string, MenuItemModelModel>();
        var all = flatList.ToList();

        // 重置 Items，避免旧数据残留
        foreach (var item in all)
        {
            // 保证 Items 不为 null 并清空旧值
            item.Items = new ObservableCollection<MenuItemModelModel>();
            if (!string.IsNullOrEmpty(item.Code))
            {
                if (!dict.ContainsKey(item.Code))
                    dict[item.Code] = item;
            }
        }

        var roots = new List<MenuItemModelModel>();

        foreach (var item in all)
        {
            // 如果 ParentCode 为空或找不到父节点，则认为是根节点
            if (string.IsNullOrWhiteSpace(item.ParentCode) || !dict.TryGetValue(item.ParentCode, out var parent) || parent == item)
            {
                roots.Add(item);
            }
            else
            {
                // 防止循环引用：如果父节点是当前节点或已在子链中则跳过
                parent.Items.Add(item);
            }
        }

        return new ObservableCollection<MenuItemModelModel>(roots);
    }
}
