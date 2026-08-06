using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.UI.Mvvm;
using Devkit.Modules.Ssamc.Configuration;
using Devkit.Modules.Ssamc.Servers;
using Devkit.Services.Interfaces;
using HandyControl.Controls;

namespace Module.Ssamc.ViewModels;

public partial class ApiUpdateViewModel : LoadingViewModelBase
{
    private readonly IApiUpdateServer _apiUpdateServer;
    private readonly IFileService _fileService;
    private readonly IModuleStorage _moduleStorage;
    private IList<string> _selectedApiCodes = new List<string>();

    public ApiUpdateViewModel(
        IApiUpdateServer apiUpdateServer,
        IFileService fileService,
        IModuleStorage moduleStorage)
    {
        _apiUpdateServer = apiUpdateServer;
        _fileService = fileService;
        _moduleStorage = moduleStorage;
    }

    [ObservableProperty]
    private string _message = "API 更新";

    [ObservableProperty]
    private string _sourceCodePath = SsamcEnvironment.SourceCodePath;

    public IList<string> SelectApiCodes
    {
        get => _selectedApiCodes;
        set => SetProperty(ref _selectedApiCodes, value);
    }

    [ObservableProperty]
    private string _strSelectApiCodes = string.Empty;

    [ObservableProperty]
    private string _codePreview = string.Empty;

    [ObservableProperty]
    private string _sourceCodePreview = string.Empty;

    [ObservableProperty]
    private string _strUpdateApis = string.Empty;

    [ObservableProperty]
    private string? _lastNotificationMessage;

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        var settings = await Task.Run(ReadSettings, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (settings == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(settings.SourceCodePath))
        {
            SourceCodePath = settings.SourceCodePath;
        }

        StrSelectApiCodes = settings.StrSelectApiCodes ?? string.Empty;
    }

    [RelayCommand]
    private Task ScanSourceCode()
    {
        return RunPageOperationAsync(async cancellationToken =>
        {
            ValidateSourcePath();

            var allApiSourceInfos = await Task.Run(
                () => _apiUpdateServer.GetAllApiSourceInfos(SourceCodePath),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            await SaveSettingsAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private Task Preview()
    {
        return RunPageOperationAsync(async cancellationToken =>
        {
            ValidateSourcePath();

            var apiCode = StrSelectApiCodes?.Trim();
            if (string.IsNullOrWhiteSpace(apiCode))
            {
                throw new InvalidOperationException("请输入要预览的 ApiCode。");
            }

            var previews = await Task.Run(() =>
            {
                var executionSource = _apiUpdateServer.GetExecutionSourceCodeByApiCode(
                    SourceCodePath,
                    apiCode);
                var extendSource = _apiUpdateServer.GetSourceCodeByApiCode(
                    SourceCodePath,
                    apiCode);
                return (executionSource, extendSource);
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            SourceCodePreview = previews.executionSource;
            CodePreview = previews.extendSource;
            await SaveSettingsAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private Task Update(object? buttonParameter)
    {
        return RunPageOperationAsync(async cancellationToken =>
        {
            ValidateSourcePath();

            var updateApiCodes = (StrUpdateApis ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (updateApiCodes.Count == 0)
            {
                ShowInformation("请输入要更新的接口");
                return;
            }

            var target = buttonParameter?.ToString();
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new InvalidOperationException("未指定数据库目标环境。");
            }

            var connectionString = SsamcEnvironment.GetDatabaseConnection(target);
            var results = await Task.Run(() => UpdateApis(
                updateApiCodes,
                connectionString,
                cancellationToken), cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            foreach (var result in results)
            {
                ShowInformation(result);
            }

            await SaveSettingsAsync(cancellationToken);
        });
    }

    private List<string> UpdateApis(
        IEnumerable<string> updateApiCodes,
        string connectionString,
        CancellationToken cancellationToken)
    {
        var apiInfos = _apiUpdateServer.GetAllApiSourceInfos(SourceCodePath);
        var results = new List<string>();

        foreach (var apiCode in updateApiCodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceCode = _apiUpdateServer.GetSourceCodeByApiCode(apiInfos, apiCode);
            var updated = _apiUpdateServer.UpdateExtendCode(apiCode, sourceCode, connectionString);
            results.Add($"{apiCode}，更新{(updated ? "成功" : "失败")}");
        }

        return results;
    }

    private async Task RunPageOperationAsync(Func<CancellationToken, Task> operation)
    {
        try
        {
            await RunWithLoadingAsync(operation);
        }
        catch (OperationCanceledException) when (LifetimeCancellationToken.IsCancellationRequested)
        {
            // The tab was closed while the command was running.
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ValidateSourcePath()
    {
        if (string.IsNullOrWhiteSpace(SourceCodePath))
        {
            throw new InvalidOperationException("请先配置源代码目录。");
        }

        if (!Directory.Exists(SourceCodePath))
        {
            throw new DirectoryNotFoundException($"源代码目录不存在：{SourceCodePath}");
        }
    }

    private Task SaveSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = new ApiUpdateSettings
        {
            SourceCodePath = SourceCodePath,
            StrSelectApiCodes = StrSelectApiCodes
        };

        return Task.Run(() =>
        {
            try
            {
                var folderPath = _moduleStorage.GetModulePath("ssamc");
                _fileService.Save(folderPath, "apiupdate.json", settings);
            }
            catch
            {
                // Local preferences must not block the requested operation.
            }
        }, cancellationToken);
    }

    private ApiUpdateSettings? ReadSettings()
    {
        var folderPath = _moduleStorage.GetModulePath("ssamc");
        return _fileService.Read<ApiUpdateSettings>(folderPath, "apiupdate.json");
    }

    private void ShowInformation(string message)
    {
        LastNotificationMessage = message;
        if (Application.Current != null)
        {
            Growl.Info(message);
        }
    }

    private void ShowError(string message)
    {
        LastNotificationMessage = message;
        if (Application.Current != null)
        {
            Growl.Error(message);
        }
    }

    private sealed class ApiUpdateSettings
    {
        public string? SourceCodePath { get; set; }

        public string? StrSelectApiCodes { get; set; }
    }
}
