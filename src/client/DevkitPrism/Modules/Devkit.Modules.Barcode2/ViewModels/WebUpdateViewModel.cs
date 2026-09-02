using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.Helpers;
using Devkit.Core.UI;
using Devkit.Core.UI.Helpers;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Mvvm;
using Devkit.Services.Interfaces.Notifications;
using Barcode2.Configuration;

namespace Barcode2.ViewModels;

public partial class WebappUpdateViewModel : ViewModelBase
{
    private readonly IBarcode2ConfigurationService _configuration;
    private readonly IClientNotificationService _notifications;

    [ObservableProperty]
    private ObservableCollection<FileNodeModel> _treeNodes = [];

    [ObservableProperty]
    private string _webappPath = string.Empty;

    public WebappUpdateViewModel(
        IBarcode2ConfigurationService configuration,
        IClientNotificationService notifications)
    {
        _configuration = configuration;
        _notifications = notifications;
    }

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            WebappPath = (await _configuration.GetAsync(cancellationToken)).WebappSourcePath;
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    [RelayCommand]
    private async Task SearchFiles(object? searchRootPath)
    {
        try
        {
            WebappPath = (await _configuration.GetAsync()).WebappSourcePath;
            var rootPath = WebappPath;
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                ShowWarning("请先配置 webapp 源目录。");
                return;
            }

            TreeNodes = new ObservableCollection<FileNodeModel>(
                FileSystemHelper.GetChildNodesRecursive(rootPath));
        }
        catch (Barcode2ConfigurationException exception)
        {
            ShowWarning(exception.Message);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    [RelayCommand]
    private async Task UpdateFiles(object? parameter)
    {
        try
        {
            var targetName = parameter as string;
            if (string.IsNullOrWhiteSpace(targetName))
            {
                ShowWarning("未指定发布目标环境。");
                return;
            }

            var settings = await _configuration.GetAsync();
            WebappPath = settings.WebappSourcePath;
            if (string.IsNullOrWhiteSpace(WebappPath))
            {
                ShowWarning("未配置 webapp 源目录。");
                return;
            }

            var files = TreeNodes
                .SelectMany(node => node.GetCheckedNodes())
                .Distinct()
                .Where(node => node.NodeType == FileNodeType.File)
                .ToList();

            if (files.Count == 0)
            {
                ShowWarning("请选择要更新的文件。");
                return;
            }

            var targets = settings.GetShareTargets(targetName)
                .Select(target => new Barcode2ShareTarget(
                    target.Root,
                    target.Username,
                    target.Password))
                .ToArray();
            var failures = new List<string>();

            foreach (var target in targets)
            {
                try
                {
                    using var uploader = new NetworkShareUploader(
                        target.Root,
                        target.Username,
                        target.Password);

                    foreach (var fileNode in files)
                    {
                        uploader.UploadFile(
                            fileNode.FullPath,
                            ExtractRelativePath(fileNode.FullPath));
                    }
                }
                catch (Exception exception)
                {
                    failures.Add($"{target.Root}: {exception.Message}");
                }
            }

            if (failures.Count > 0)
            {
                ShowError(
                    $"{targetName} 更新未全部完成（失败 {failures.Count}/{targets.Length}）：" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, failures));
                return;
            }

            ShowInformation($"{targetName}，已成功更新 {targets.Length} 个目标。");
        }
        catch (Barcode2ConfigurationException exception)
        {
            ShowWarning(exception.Message);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    public static string ExtractRelativePath(string fullPath, string baseFolderName = "webapp")
    {
        if (string.IsNullOrEmpty(fullPath))
            return fullPath;

        var searchKey = baseFolderName + @"\";
        var index = fullPath.LastIndexOf(searchKey, StringComparison.OrdinalIgnoreCase);

        return index >= 0
                   ? fullPath[(index + searchKey.Length)..]
                   : fullPath;
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
