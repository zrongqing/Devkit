using System.Collections.ObjectModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Mvvm;
using Devkit.Core.UI.Services;
using Devkit.Prism.Events;
using Devkit.Services.Interfaces;

namespace Devkit.ViewModels;

public partial class MenuViewModel : ViewModelBase
{
    private readonly IEventAggregator _eventAggregator;
    private readonly IShellService _shellService;
    private bool _isSynchronizingActiveMenu;

    public MenuViewModel(IContainerProvider container)
    {
        _shellService = container.Resolve<IShellService>();
        _eventAggregator = container.Resolve<IEventAggregator>();
        _eventAggregator.GetEvent<MenuActiveEvent>().Subscribe(SynchronizeActiveMenu);
    }

    [ObservableProperty]
    private MenuItemModel? _activeMenuItemModel;
    [ObservableProperty]
    private ListCollectionView _collectionView;
    [ObservableProperty]
    private ObservableCollection<MenuItemModel> _menus = new();

    partial void OnActiveMenuItemModelChanged(MenuItemModel? value)
    {
        if (!_isSynchronizingActiveMenu)
        {
            MenuClicked(value);
        }
    }

    private void SynchronizeActiveMenu(TabItemModel? tab)
    {
        try
        {
            _isSynchronizingActiveMenu = true;
            ActiveMenuItemModel = tab == null ? null : _shellService.FindMenu(tab.MenuId);
        }
        finally
        {
            _isSynchronizingActiveMenu = false;
        }
    }

    [RelayCommand]
    private void Loaded()
    {
        var menus = _shellService.LoadMenus().ToList();
        CollectionView = new ListCollectionView(menus);
    }
    [RelayCommand]
    private void MenuClicked(MenuItemModel? menu)
    {
        if (menu == null || string.IsNullOrWhiteSpace(menu.ViewName) && menu.ViewModelType == null)
        {
            return;
        }
        
        _eventAggregator.GetEvent<MenuClickEvent>().Publish(menu.Id);
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

            OnPropertyChanged(nameof(FilterText));
        }
    }
    #endregion
}
