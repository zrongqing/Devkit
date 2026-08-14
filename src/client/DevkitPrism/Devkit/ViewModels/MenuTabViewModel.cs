using System.Collections.ObjectModel;
using System.Windows;
using Devkit.Core.UI.Contracts;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Mvvm;
using Devkit.Core.UI.Services;
using Devkit.Prism.Events;
using Devkit.Services.Interfaces.Logging;

namespace Devkit.ViewModels;

public class MenuTabViewModel : BindableBase
{
    private readonly IEventAggregator _eventAggregator;
    private readonly IShellService _shellService;
    private readonly DelayedLoadingState _globalLoading;
    private readonly IClientLogger _logger;
    private readonly Dictionary<TabItemModel, CancellationTokenSource> _loadCancellations = new();
    private SubscriptionToken? _menuClickSubscription;
    private TabItemModel? _selectedTab;

    public MenuTabViewModel(
        IEventAggregator eventAggregator,
        IShellService shellService,
        DelayedLoadingState globalLoading,
        IClientLogger logger)
    {
        _eventAggregator = eventAggregator;
        _shellService = shellService;
        _globalLoading = globalLoading;
        _logger = logger;
        LoadedCommand = new DelegateCommand(OnLoaded);
        UnloadedCommand = new DelegateCommand(OnUnloaded);
        CloseTabCommand = new DelegateCommand<TabItemModel?>(CloseTab);
        RetryTabCommand = new DelegateCommand<TabItemModel?>(RetryTab);
    }

    public ObservableCollection<TabItemModel> Tabs { get; } = new();

    public TabItemModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                _eventAggregator.GetEvent<MenuActiveEvent>().Publish(value);
            }
        }
    }

    public DelegateCommand LoadedCommand { get; }
    public DelegateCommand UnloadedCommand { get; }
    public DelegateCommand<TabItemModel?> CloseTabCommand { get; }
    public DelegateCommand<TabItemModel?> RetryTabCommand { get; }

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
        if (_menuClickSubscription != null)
        {
            _eventAggregator.GetEvent<MenuClickEvent>().Unsubscribe(_menuClickSubscription);
            _menuClickSubscription = null;
        }

        foreach (var cancellation in _loadCancellations.Values.ToArray())
        {
            cancellation.Cancel();
        }
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

        var tabCode = menu.AllowMultipleTabs ? $"{menu.Id}:{Guid.NewGuid():N}" : menu.Id;
        var tab = new TabItemModel(tabCode, menu.Title)
        {
            MenuId = menu.Id,
            CanClose = menu.IsClosable,
            CloseButtonState = menu.IsClosable ? Visibility.Visible : Visibility.Collapsed,
            IsSelected = true
        };

        Tabs.Add(tab);
        SelectedTab = tab;
        _ = LoadTabAsync(tab, menu, replaceContent: false);
    }

    private async Task LoadTabAsync(TabItemModel tab, MenuItemModel menu, bool replaceContent)
    {
        if (_loadCancellations.ContainsKey(tab))
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _loadCancellations[tab] = cancellation;
        tab.LoadErrorMessage = null;

        if (replaceContent)
        {
            DestroyContent(tab.Content);
            tab.Content = null;
        }

        try
        {
            cancellation.Token.ThrowIfCancellationRequested();

            var content = _shellService.ResolveContent(menu)
                ?? throw new InvalidOperationException($"无法创建模块“{menu.Title}”。");

            cancellation.Token.ThrowIfCancellationRequested();
            tab.Content = content;

            var asyncLoadable = content as IAsyncLoadable;
            if (content is FrameworkElement element && element.DataContext is IAsyncLoadable viewModelLoadable)
            {
                asyncLoadable = viewModelLoadable;
            }

            if (asyncLoadable is IUsesPageLoading)
            {
                await asyncLoadable.InitializeAsync(cancellation.Token);
            }
            else if (asyncLoadable != null)
            {
                await _globalLoading.RunAsync(
                    asyncLoadable.InitializeAsync,
                    cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Closing a tab or the shell cancels initialization without showing an error.
        }
        catch (Exception exception)
        {
            if (Tabs.Contains(tab))
            {
                tab.LoadErrorMessage = $"模块加载失败：{exception.Message}";
            }

            _logger.Error(
                exception,
                "Failed to initialize menu module {MenuId}.",
                menu.Id);
        }
        finally
        {
            if (_loadCancellations.TryGetValue(tab, out var current) && ReferenceEquals(current, cancellation))
            {
                _loadCancellations.Remove(tab);
            }

            cancellation.Dispose();
        }
    }

    private void RetryTab(TabItemModel? tab)
    {
        if (tab == null || _loadCancellations.ContainsKey(tab))
        {
            return;
        }

        var menu = _shellService.FindMenu(tab.MenuId);
        if (menu == null)
        {
            tab.LoadErrorMessage = "模块配置已不存在，无法重试。";
            return;
        }

        _ = LoadTabAsync(tab, menu, replaceContent: true);
    }

    private void CloseTab(TabItemModel? tab)
    {
        if (tab == null || !tab.CanClose)
        {
            return;
        }

        if (_loadCancellations.Remove(tab, out var cancellation))
        {
            cancellation.Cancel();
        }

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        DestroyContent(tab.Content);

        if (SelectedTab == tab)
        {
            SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Max(0, Math.Min(index, Tabs.Count - 1))];
        }
    }

    private static void DestroyContent(object? content)
    {
        if (content is FrameworkElement { DataContext: IDestructible destructibleViewModel })
        {
            destructibleViewModel.Destroy();
            return;
        }

        if (content is IDestructible destructible)
        {
            destructible.Destroy();
        }
    }
}
