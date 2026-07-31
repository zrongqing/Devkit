using System.Collections.ObjectModel;
using System.Windows;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Services;
using Devkit.Prism.Events;

namespace Devkit.ViewModels;

public class MenuTabViewModel : BindableBase
{
    private readonly IEventAggregator _eventAggregator;
    private readonly IShellService _shellService;
    private SubscriptionToken? _menuClickSubscription;
    private TabItemModel? _selectedTab;

    public MenuTabViewModel(IEventAggregator eventAggregator, IShellService shellService)
    {
        _eventAggregator = eventAggregator;
        _shellService = shellService;
        LoadedCommand = new DelegateCommand(OnLoaded);
        UnloadedCommand = new DelegateCommand(OnUnloaded);
        CloseTabCommand = new DelegateCommand<TabItemModel?>(CloseTab);
    }

    public ObservableCollection<TabItemModel> Tabs { get; } = new();

    public TabItemModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value) && value != null)
            {
                _eventAggregator.GetEvent<MenuActiveEvent>().Publish(value);
            }
        }
    }

    public DelegateCommand LoadedCommand { get; }
    public DelegateCommand UnloadedCommand { get; }
    public DelegateCommand<TabItemModel?> CloseTabCommand { get; }

    private void OnLoaded()
    {
        _menuClickSubscription ??= _eventAggregator.GetEvent<MenuClickEvent>().Subscribe(OpenMenu);

        if (Tabs.Count == 0)
        {
            OpenMenu("home");
        }
    }

    private void OnUnloaded()
    {
        if (_menuClickSubscription == null)
        {
            return;
        }

        _eventAggregator.GetEvent<MenuClickEvent>().Unsubscribe(_menuClickSubscription);
        _menuClickSubscription = null;
    }

    private void OpenMenu(string menuId)
    {
        var menu = _shellService.FindMenu(menuId);
        if (menu == null)
        {
            return;
        }

        if (!menu.AllowMultipleTabs)
        {
            var existing = Tabs.FirstOrDefault(x => x.MenuId == menu.Id);
            if (existing != null)
            {
                SelectedTab = existing;
                return;
            }
        }

        var content = _shellService.ResolveContent(menu);
        if (content == null)
        {
            return;
        }

        var tabCode = menu.AllowMultipleTabs ? $"{menu.Id}:{Guid.NewGuid():N}" : menu.Id;
        var tab = new TabItemModel(tabCode, menu.Title, content)
        {
            MenuId = menu.Id,
            CanClose = menu.IsClosable,
            CloseButtonState = menu.IsClosable ? Visibility.Visible : Visibility.Collapsed,
            IsSelected = true
        };

        Tabs.Add(tab);
        SelectedTab = tab;
    }

    private void CloseTab(TabItemModel? tab)
    {
        if (tab == null || !tab.CanClose)
        {
            return;
        }

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (SelectedTab == tab)
        {
            SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Max(0, Math.Min(index, Tabs.Count - 1))];
        }
    }
}
