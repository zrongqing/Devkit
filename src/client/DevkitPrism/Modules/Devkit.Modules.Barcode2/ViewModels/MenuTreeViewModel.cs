using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.UI.Contracts;
using Devkit.Core.UI.Mvvm;
using Devkit.Services.Interfaces.Notifications;
using Barcode2.Configuration;
using Barcode2.Models;
using Barcode2.Servers;

namespace Barcode2.ViewModels;

public partial class MenuTreeViewModel : LoadingViewModelBase, IUsesPageLoading
{
    private readonly IBarcode2ConfigurationService _configuration;
    private readonly IMenuTreeDataSource _menuTreeDataSource;
    private readonly IClientNotificationService _notifications;
    private readonly IWebPageLauncher _webPageLauncher;
    private IReadOnlyList<MenuTreeItem> _allItems = [];

    [ObservableProperty]
    private IReadOnlyList<Barcode2PageEnvironment> _environmentOptions = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private MenuDetails? _selectedDetails;

    [ObservableProperty]
    private Barcode2PageEnvironment? _selectedEnvironment;

    [ObservableProperty]
    private MenuTreeNode? _selectedMenu;

    [ObservableProperty]
    private ObservableCollection<MenuTreeNode> _treeNodes = [];

    public MenuTreeViewModel(
        IMenuTreeDataSource menuTreeDataSource,
        IWebPageLauncher webPageLauncher,
        IBarcode2ConfigurationService configuration,
        IClientNotificationService notifications)
    {
        _menuTreeDataSource = menuTreeDataSource;
        _webPageLauncher = webPageLauncher;
        _configuration = configuration;
        _notifications = notifications;
    }

    public bool HasTreeNodes => TreeNodes.Count > 0;

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _configuration.GetAsync(cancellationToken);
            EnvironmentOptions = settings.Environments
                .Select(environment => new Barcode2PageEnvironment(
                    environment.Key,
                    environment.DisplayName,
                    environment.PageBaseAddress))
                .ToArray();
            SelectedEnvironment = EnvironmentOptions.FirstOrDefault(environment =>
                                      environment.Key.Equals(
                                          settings.SelectedPageEnvironmentKey,
                                          StringComparison.OrdinalIgnoreCase))
                                  ?? EnvironmentOptions.First(environment =>
                                      environment.Key == Barcode2Defaults.DevelopmentEnvironment);
        }
        catch (Exception exception)
        {
            HandleOperationError(exception);
            return;
        }

        await LoadMenuTreeAsync();
    }

    [RelayCommand]
    private void Search()
    {
        ApplyFilter();
    }

    [RelayCommand]
    private Task Reload()
    {
        return LoadMenuTreeAsync();
    }

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private async Task Open(MenuTreeNode? node)
    {
        node ??= SelectedMenu;
        if (node?.MainPageId is not > 0)
        {
            ShowWarning("该菜单模块未配置主页面 ID，无法打开页面。");
            return;
        }

        if (SelectedEnvironment is null)
        {
            ShowWarning("请选择页面环境。");
            return;
        }

        await RunWithLoadingAsync(async cancellationToken =>
        {
            var latestSettings = await _configuration.GetAsync(cancellationToken);
            var latestEnvironment = latestSettings.GetEnvironment(SelectedEnvironment.Key);
            Barcode2ConfigurationService.ValidatePageAddress(latestEnvironment);
            var request = new WebTabOpenRequest(
                node.Name,
                latestEnvironment.PageBaseAddress,
                node.MainPageId.Value);
            await _webPageLauncher.OpenTabAsync(request, cancellationToken);
            await SaveSelectedEnvironmentAsync(cancellationToken);
            ShowInformation($"已在{SelectedEnvironment.DisplayName}打开：{node.Name}");
        }, HandleOperationError);
    }

    private bool CanOpen(MenuTreeNode? node)
    {
        return (node ?? SelectedMenu)?.CanOpen == true && SelectedEnvironment is not null;
    }

    partial void OnTreeNodesChanged(ObservableCollection<MenuTreeNode> value)
    {
        OnPropertyChanged(nameof(HasTreeNodes));
    }

    partial void OnSelectedMenuChanged(MenuTreeNode? value)
    {
        SelectedDetails = value?.Details;
        OpenCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedEnvironmentChanged(Barcode2PageEnvironment? value)
    {
        OpenCommand.NotifyCanExecuteChanged();
    }

    private Task LoadMenuTreeAsync()
    {
        return RunWithLoadingAsync(async cancellationToken =>
        {
            if (SelectedEnvironment is null)
            {
                throw new InvalidOperationException("请选择菜单数据库环境。");
            }

            await SaveSelectedEnvironmentAsync(cancellationToken);

            _allItems = await _menuTreeDataSource.GetMenuTreeAsync(
                            SelectedEnvironment.Key,
                            cancellationToken) ?? [];
            cancellationToken.ThrowIfCancellationRequested();
            ApplyFilter();

            if (_allItems.Count == 0)
            {
                ShowWarning("未找到 IS_DELETE = 0 的菜单数据。");
            }
        }, HandleOperationError);
    }

    private void ApplyFilter()
    {
        var keyword = SearchText.Trim();
        var nodes = string.IsNullOrEmpty(keyword)
                        ? _allItems.Select(item => CreateNode(item, false))
                        : _allItems
                            .Select(item => CreateFilteredNode(item, keyword, false))
                            .Where(node => node is not null)
                            .Select(node => node!);

        SelectedMenu = null;
        TreeNodes = new ObservableCollection<MenuTreeNode>(nodes);
    }

    private static MenuTreeNode? CreateFilteredNode(
        MenuTreeItem item,
        string keyword,
        bool ancestorMatched)
    {
        var matches = Matches(item, keyword);
        if (ancestorMatched || matches)
        {
            return CreateNode(item, true);
        }

        var children = item.Children
            .Select(child => CreateFilteredNode(child, keyword, false))
            .Where(node => node is not null)
            .Select(node => node!)
            .ToList();

        return children.Count == 0
                   ? null
                   : CreateNode(item, true, children);
    }

    private static bool Matches(MenuTreeItem item, string keyword)
    {
        return Contains(item.MenuId.ToString(), keyword) ||
               Contains(item.Name, keyword) ||
               Contains(item.Code, keyword) ||
               Contains(item.ParentName, keyword) ||
               Contains(item.ModuleName, keyword) ||
               Contains(item.ModuleId?.ToString(), keyword) ||
               Contains(item.MainPageName, keyword) ||
               Contains(item.MainPageId?.ToString(), keyword);
    }

    private static bool Contains(string? value, string keyword)
    {
        return value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static MenuTreeNode CreateNode(
        MenuTreeItem item,
        bool expand,
        IEnumerable<MenuTreeNode>? children = null)
    {
        return new MenuTreeNode
        {
            MenuId = item.MenuId,
            ParentMenuId = item.ParentMenuId,
            Name = item.Name,
            Code = item.Code,
            ParentName = item.ParentName,
            ModuleId = item.ModuleId,
            ModuleName = item.ModuleName,
            MainPageId = item.MainPageId,
            MainPageName = item.MainPageName,
            IsExpanded = expand,
            Children = new ObservableCollection<MenuTreeNode>(
                children ?? item.Children.Select(child => CreateNode(child, expand)))
        };
    }

    private async Task SaveSelectedEnvironmentAsync(CancellationToken cancellationToken)
    {
        if (SelectedEnvironment is null)
        {
            return;
        }

        var settings = await _configuration.GetAsync(cancellationToken);
        settings.SelectedPageEnvironmentKey = SelectedEnvironment.Key;
        await _configuration.SaveAsync(settings, cancellationToken);
    }

    private void HandleOperationError(Exception exception)
    {
        if (exception is Barcode2ConfigurationException)
        {
            ShowWarning(exception.Message);
            return;
        }

        ShowError(exception.Message);
    }

    private void ShowInformation(string message)
    {
        _notifications.Show(new NotificationRequest
        {
            Message = message,
            Level = NotificationLevel.Info
        });
    }

    private void ShowWarning(string message)
    {
        _notifications.Show(new NotificationRequest
        {
            Message = message,
            Level = NotificationLevel.Warning
        });
    }

    private void ShowError(string message)
    {
        _notifications.Show(new NotificationRequest
        {
            Message = message,
            Level = NotificationLevel.Error
        });
    }
}
