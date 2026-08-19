using System.Collections.ObjectModel;
using System.Windows;
using Devkit.Core.UI.Contracts;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Mvvm;
using Devkit.Core.UI.Services;
using Devkit.Modules;
using Devkit.Prism.Events;
using Devkit.Services.Interfaces.Logging;

namespace Devkit.ViewModels;

public class MenuTabViewModel : BindableBase
{
    private readonly IEventAggregator _eventAggregator;
    private readonly IShellService _shellService;
    private readonly DelayedLoadingState _globalLoading;
    private readonly IClientLogger _logger;
    private readonly IModuleContentCoordinator _contentCoordinator;
    private readonly Dictionary<TabItemModel, TabLoadOperation> _loadOperations = new();
    private SubscriptionToken? _menuClickSubscription;
    private TabItemModel? _selectedTab;

    public MenuTabViewModel(
        IEventAggregator eventAggregator,
        IShellService shellService,
        DelayedLoadingState globalLoading,
        IClientLogger logger,
        IModuleContentCoordinator? contentCoordinator = null)
    {
        _eventAggregator = eventAggregator;
        _shellService = shellService;
        _globalLoading = globalLoading;
        _logger = logger;
        _contentCoordinator = contentCoordinator ?? new ModuleContentCoordinator();
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

    public int GetOpenModuleContentCount(string moduleId) =>
        Tabs.Count(tab => string.Equals(tab.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));

    public async Task CloseModuleContentAsync(string moduleId)
    {
        var tabs = Tabs
            .Where(tab => string.Equals(tab.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var tab in tabs)
        {
            await CloseTabCoreAsync(tab, force: true);
        }
    }

    private void OnLoaded()
    {
        _menuClickSubscription ??= _eventAggregator.GetEvent<MenuClickEvent>().Subscribe(OpenMenu);
        _contentCoordinator.Attach(this);

        if (Tabs.Count == 0)
        {
            OpenMenu("home");
        }
    }

    private void OnUnloaded()
    {
        _contentCoordinator.Detach(this);
        if (_menuClickSubscription != null)
        {
            _eventAggregator.GetEvent<MenuClickEvent>().Unsubscribe(_menuClickSubscription);
            _menuClickSubscription = null;
        }

        foreach (var operation in _loadOperations.Values.ToArray())
        {
            operation.Cancellation.Cancel();
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
            var existing = Tabs.FirstOrDefault(tab => tab.MenuId == menu.Id);
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
            ModuleId = menu.ModuleId,
            CanClose = menu.IsClosable,
            CloseButtonState = menu.IsClosable ? Visibility.Visible : Visibility.Collapsed,
            IsSelected = true
        };

        Tabs.Add(tab);
        SelectedTab = tab;
        BeginLoad(tab, menu, replaceContent: false);
    }

    private void BeginLoad(TabItemModel tab, MenuItemModel menu, bool replaceContent)
    {
        if (_loadOperations.ContainsKey(tab))
        {
            return;
        }

        var operation = new TabLoadOperation();
        _loadOperations[tab] = operation;
        operation.Completion = LoadTabAsync(tab, menu, replaceContent, operation);
    }

    private async Task LoadTabAsync(
        TabItemModel tab,
        MenuItemModel menu,
        bool replaceContent,
        TabLoadOperation operation)
    {
        var cancellation = operation.Cancellation;
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
                await _globalLoading.RunAsync(asyncLoadable.InitializeAsync, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Closing a tab or unloading its module cancels initialization silently.
        }
        catch (Exception exception)
        {
            if (Tabs.Contains(tab))
            {
                tab.LoadErrorMessage = $"模块加载失败：{exception.Message}";
            }

            _logger.Error(exception, "Failed to initialize menu module {MenuId}.", menu.Id);
        }
        finally
        {
            if (_loadOperations.TryGetValue(tab, out var current) && ReferenceEquals(current, operation))
            {
                _loadOperations.Remove(tab);
            }

            cancellation.Dispose();
        }
    }

    private void RetryTab(TabItemModel? tab)
    {
        if (tab == null || _loadOperations.ContainsKey(tab))
        {
            return;
        }

        var menu = _shellService.FindMenu(tab.MenuId);
        if (menu == null)
        {
            tab.LoadErrorMessage = "模块配置已不存在，无法重试。";
            return;
        }

        BeginLoad(tab, menu, replaceContent: true);
    }

    private async void CloseTab(TabItemModel? tab)
    {
        if (tab != null)
        {
            await CloseTabCoreAsync(tab, force: false);
        }
    }

    private async Task CloseTabCoreAsync(TabItemModel tab, bool force)
    {
        if (!force && !tab.CanClose)
        {
            return;
        }

        _loadOperations.TryGetValue(tab, out var operation);
        operation?.Cancellation.Cancel();

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        if (SelectedTab == tab)
        {
            SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Max(0, Math.Min(index, Tabs.Count - 1))];
        }

        if (operation != null)
        {
            await operation.Completion;
        }

        DestroyContent(tab.Content);
        tab.Content = null;
    }

    private static void DestroyContent(object? content)
    {
        if (content is FrameworkElement element)
        {
            if (element.DataContext is IDestructible destructibleViewModel)
            {
                destructibleViewModel.Destroy();
            }

            element.DataContext = null;
        }
        else if (content is IDestructible destructible)
        {
            destructible.Destroy();
        }
    }

    private sealed class TabLoadOperation
    {
        public CancellationTokenSource Cancellation { get; } = new();

        public Task Completion { get; set; } = Task.CompletedTask;
    }
}
