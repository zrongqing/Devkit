using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.UI.Contracts;
using Devkit.Core.UI.Mvvm;
using Devkit.Services.Interfaces;
using Devkit.Services.Interfaces.Notifications;
using Ssamc.Configuration;
using Ssamc.Models;
using Ssamc.Servers;

namespace Ssamc.ViewModels;

public partial class MenuTreeViewModel : LoadingViewModelBase, IUsesPageLoading
{
    private const string ModuleName = "ssamc";
    private const string SettingsFileName = "menu-tree.json";
    private readonly IFileService _fileService;
    private readonly IMenuTreeDataSource _menuTreeDataSource;
    private readonly IModuleStorage _moduleStorage;
    private readonly IClientNotificationService _notifications;
    private readonly IWebPageLauncher _webPageLauncher;
    private IReadOnlyList<MenuTreeItem> _allItems = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private MenuDetails? _selectedDetails;

    [ObservableProperty]
    private SsamcPageEnvironment? _selectedEnvironment;

    [ObservableProperty]
    private MenuTreeNode? _selectedMenu;

    [ObservableProperty]
    private ObservableCollection<MenuTreeNode> _treeNodes = [];

    public MenuTreeViewModel(
        IMenuTreeDataSource menuTreeDataSource,
        IWebPageLauncher webPageLauncher,
        IFileService fileService,
        IModuleStorage moduleStorage,
        IClientNotificationService notifications)
    {
        _menuTreeDataSource = menuTreeDataSource;
        _webPageLauncher = webPageLauncher;
        _fileService = fileService;
        _moduleStorage = moduleStorage;
        _notifications = notifications;
        EnvironmentOptions = SsamcEnvironment.GetPageEnvironments();
        _selectedEnvironment = EnvironmentOptions.First(environment =>
            environment.Key == SsamcEnvironment.DevelopmentEnvironment);
    }

    public IReadOnlyList<SsamcPageEnvironment> EnvironmentOptions { get; }

    public bool HasTreeNodes => TreeNodes.Count > 0;

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        var settings = await Task.Run(ReadSettings, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(settings?.PageEnvironmentKey))
        {
            SelectedEnvironment = EnvironmentOptions.FirstOrDefault(environment =>
                                      environment.Key.Equals(
                                          settings.PageEnvironmentKey,
                                          StringComparison.OrdinalIgnoreCase))
                               ?? SelectedEnvironment;
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
            var request = new WebTabOpenRequest(
                node.Name,
                SelectedEnvironment.BaseAddress,
                node.MainPageId.Value);
            await _webPageLauncher.OpenTabAsync(request, cancellationToken);
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

    partial void OnSelectedEnvironmentChanged(SsamcPageEnvironment? value)
    {
        OpenCommand.NotifyCanExecuteChanged();
        SaveSettings();
    }

    private Task LoadMenuTreeAsync()
    {
        return RunWithLoadingAsync(async cancellationToken =>
        {
            if (SelectedEnvironment is null)
            {
                throw new InvalidOperationException("请选择菜单数据库环境。");
            }

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

    private void SaveSettings()
    {
        if (SelectedEnvironment is null)
        {
            return;
        }

        try
        {
            var folderPath = _moduleStorage.GetModulePath(ModuleName);
            var settings = ReadSettings() ?? new MenuTreeSettings();
            settings.PageEnvironmentKey = SelectedEnvironment.Key;
            _fileService.Save(folderPath, SettingsFileName, settings);
        }
        catch
        {
            // Local preferences must not block menu navigation.
        }
    }

    private MenuTreeSettings? ReadSettings()
    {
        try
        {
            var folderPath = _moduleStorage.GetModulePath(ModuleName);
            return _fileService.Read<MenuTreeSettings>(folderPath, SettingsFileName);
        }
        catch
        {
            return null;
        }
    }

    private void HandleOperationError(Exception exception)
    {
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
