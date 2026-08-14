using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.Helpers;
using Devkit.Core.UI;
using Devkit.Core.UI.Helpers;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Mvvm;
using Ssamc.Configuration;
using Devkit.Services.Interfaces;
using HandyControl.Controls;

namespace Ssamc.ViewModels;

public partial class WebappUpdateViewModel : ViewModelBase
{
    private readonly IFileService _fileService;
    private readonly IModuleStorage _moduleStorage;

    [ObservableProperty]
    private string _webappPath = SsamcEnvironment.WebappSourcePath;

    [ObservableProperty]
    private ObservableCollection<FileNodeModel> _treeNodes = [];

    public WebappUpdateViewModel(IFileService fileService, IModuleStorage moduleStorage)
    {
        _fileService = fileService;
        _moduleStorage = moduleStorage;
    }

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        var settings = await Task.Run(ReadSettings, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(settings?.WebappPath))
        {
            WebappPath = settings.WebappPath;
        }
    }

    [RelayCommand]
    private void SearchFiles(object? searchRootPath)
    {
        try
        {
            var rootPath = searchRootPath as string;
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                Growl.Info("请先配置 webapp 源目录。");
                return;
            }

            TreeNodes = new ObservableCollection<FileNodeModel>(
                FileSystemHelper.GetChildNodesRecursive(rootPath));
            SaveFile();
        }
        catch (Exception exception)
        {
            Growl.Error(exception.Message);
        }
    }

    [RelayCommand]
    private void UpdateFiles(object? parameter)
    {
        try
        {
            var targetName = parameter as string;
            if (string.IsNullOrWhiteSpace(targetName))
                throw new InvalidOperationException("未指定发布目标环境。");

            if (string.IsNullOrWhiteSpace(WebappPath))
                throw new InvalidOperationException("未配置 webapp 源目录。");

            var files = TreeNodes
                .SelectMany(node => node.GetCheckedNodes())
                .Distinct()
                .Where(node => node.NodeType == FileNodeType.File)
                .ToList();

            if (files.Count == 0)
            {
                Growl.Info("请选择要更新的文件。");
                return;
            }

            foreach (var target in SsamcEnvironment.GetShareTargets(targetName))
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

            Growl.Success($"{targetName}，更新成功");
        }
        catch (Exception exception)
        {
            Growl.Error(exception.Message);
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

    private void SaveFile()
    {
        try
        {
            var folderPath = _moduleStorage.GetModulePath("ssamc");
            _fileService.Save(
                folderPath,
                $"{nameof(WebappUpdateViewModel)}.json",
                new WebappUpdateSettings { WebappPath = WebappPath });
        }
        catch
        {
            // 本地偏好保存失败不阻断发布操作。
        }
    }

    private WebappUpdateSettings? ReadSettings()
    {
        try
        {
            var folderPath = _moduleStorage.GetModulePath("ssamc");
            return _fileService.Read<WebappUpdateSettings>(
                folderPath,
                $"{nameof(WebappUpdateViewModel)}.json");
        }
        catch
        {
            // 无历史配置时使用环境变量或空值。
            return null;
        }
    }

    private sealed class WebappUpdateSettings
    {
        public string WebappPath { get; set; } = string.Empty;
    }
}
