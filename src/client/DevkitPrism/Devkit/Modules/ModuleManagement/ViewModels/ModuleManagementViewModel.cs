using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.UI.Mvvm;
using Devkit.Services.Interfaces.Dialogs;
using Devkit.Services.Interfaces.Notifications;
using Microsoft.Win32;

namespace Devkit.Modules.ModuleManagement.ViewModels;

public partial class ModuleManagementViewModel : ViewModelBase
{
    private readonly DynamicModuleManager _moduleManager;
    private readonly IModuleContentCoordinator _contentCoordinator;
    private readonly IConfirmationDialogService _confirmationDialog;
    private readonly IClientNotificationService _notifications;

    public ModuleManagementViewModel(
        DynamicModuleManager moduleManager,
        IModuleContentCoordinator contentCoordinator,
        IConfirmationDialogService confirmationDialog,
        IClientNotificationService notifications)
    {
        _moduleManager = moduleManager;
        _contentCoordinator = contentCoordinator;
        _confirmationDialog = confirmationDialog;
        _notifications = notifications;
        ModuleDirectory = moduleManager.ModuleDirectory;
        _moduleManager.Changed += OnModulesChanged;
        RefreshItems();
    }

    public string ModuleDirectory { get; }

    public ObservableCollection<DynamicModuleDescriptor> Modules { get; } = [];

    [RelayCommand]
    private void Refresh()
    {
        _moduleManager.Refresh();
        RefreshItems();
    }

    [RelayCommand]
    private async Task AddModuleAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 Prism 模块",
            Filter = "模块程序集 (*.dll)|*.dll|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(Application.Current.MainWindow) != true)
        {
            return;
        }

        try
        {
            var module = await _moduleManager.AddAndLoadExternalAsync(dialog.FileName);
            Notify($"模块 {module.ModuleId} 已加载。", NotificationLevel.Info);
        }
        catch (Exception exception)
        {
            Notify($"模块加载失败：{exception.Message}", NotificationLevel.Error);
        }
    }

    [RelayCommand]
    private async Task LoadModuleAsync(DynamicModuleDescriptor? module)
    {
        if (module == null || module.IsLoaded || !module.IsAvailable)
        {
            return;
        }

        try
        {
            await _moduleManager.LoadAsync(module.ModuleId);
            Notify($"模块 {module.ModuleId} 已加载。", NotificationLevel.Info);
        }
        catch (Exception exception)
        {
            Notify($"模块加载失败：{exception.Message}", NotificationLevel.Error);
        }
    }

    [RelayCommand]
    private async Task UnloadModuleAsync(DynamicModuleDescriptor? module)
    {
        if (module == null || !module.IsLoaded)
        {
            return;
        }

        var openContentCount = _contentCoordinator.GetOpenContentCount(module.ModuleId);
        if (openContentCount > 0)
        {
            var confirmed = await _confirmationDialog.ConfirmAsync(
                $"模块 {module.ModuleId} 仍有 {openContentCount} 个打开页面或正在执行的任务。继续将取消任务、关闭页面并等待清理完成。",
                "卸载模块",
                "继续卸载",
                "取消");
            if (!confirmed)
            {
                return;
            }
        }

        try
        {
            await _contentCoordinator.CloseModuleContentAsync(module.ModuleId);
            await _moduleManager.UnloadAsync(module.ModuleId);
            Notify($"模块 {module.ModuleId} 已卸载，磁盘文件保持不变。", NotificationLevel.Info);
        }
        catch (Exception exception)
        {
            Notify($"模块卸载失败：{exception.Message}", NotificationLevel.Error);
        }
    }

    protected override void OnDestroy()
    {
        _moduleManager.Changed -= OnModulesChanged;
        base.OnDestroy();
    }

    private void OnModulesChanged(object? sender, EventArgs eventArgs)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            RefreshItems();
        }
        else
        {
            _ = Application.Current.Dispatcher.BeginInvoke(RefreshItems);
        }
    }

    private void RefreshItems()
    {
        var modules = _moduleManager.Modules;
        Modules.Clear();
        foreach (var module in modules)
        {
            Modules.Add(module);
        }
    }

    private void Notify(string message, NotificationLevel level) =>
        _notifications.Show(new NotificationRequest
        {
            Title = "模块管理",
            Message = message,
            Level = level
        });
}
