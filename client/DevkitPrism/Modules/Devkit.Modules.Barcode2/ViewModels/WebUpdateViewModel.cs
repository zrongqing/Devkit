using System.Collections.ObjectModel;
using System.IO;
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

    [ObservableProperty]
    private string _webappPublishPath = string.Empty;

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
            var settings = await _configuration.GetAsync(cancellationToken);
            WebappPath = settings.WebappUpdateSourcePath;
            WebappPublishPath = settings.WebappPublishPath;
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    [RelayCommand]
    private async Task SavePaths()
    {
        try
        {
            var settings = await _configuration.GetAsync();
            settings.WebappUpdateSourcePath = WebappPath.Trim();
            settings.WebappPublishPath = WebappPublishPath.Trim();
            await _configuration.SaveAsync(settings);
            ShowInformation("Webapp 路径已保存到本地。");
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

    partial void OnWebappPathChanged(string value)
    {
        TreeNodes.Clear();
    }

    [RelayCommand]
    private void SearchFiles()
    {
        try
        {
            var rootPath = WebappPath.Trim();
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                ShowWarning("请先配置 webapp 源目录。");
                return;
            }

            if (!Directory.Exists(rootPath))
            {
                ShowWarning("webapp 源目录不存在或无法访问。");
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

            var sourcePath = WebappPath.Trim();
            var publishPath = WebappPublishPath.Trim();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                ShowWarning("未配置 webapp 源目录。");
                return;
            }

            var files = TreeNodes
                .SelectMany(node => node.GetCheckedNodes())
                .Distinct()
                .Where(node => node.NodeType == FileNodeType.File)
                .Select(node => new
                {
                    node.FullPath,
                    RelativePath = ExtractRelativePath(node.FullPath, sourcePath)
                })
                .ToList();

            if (files.Count == 0)
            {
                ShowWarning("请选择要更新的文件。");
                return;
            }

            var settings = await _configuration.GetAsync();
            var targets = settings.GetShareTargets(targetName, publishPath)
                .Select(target => new Barcode2ShareTarget(
                    target.Root,
                    target.Username,
                    target.Password))
                .Distinct()
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
                            fileNode.RelativePath);
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

    public static string ExtractRelativePath(string fullPath, string sourceRootPath)
    {
        var relativePath = Path.GetRelativePath(sourceRootPath, fullPath);
        if (Path.IsPathRooted(relativePath) || relativePath == ".." ||
            relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new Barcode2ConfigurationException("所选文件不在当前 webapp 源目录内，请重新读取文件夹。");
        }

        return relativePath;
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
