using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.UI.Mvvm;
using Devkit.Services.Interfaces;
using Devkit.Services.Interfaces.Notifications;
using Ssamc.Configuration;
using Ssamc.Servers;

namespace Ssamc.ViewModels;

public partial class ApiUpdateViewModel : LoadingViewModelBase
{
    private readonly IApiUpdateServer _apiUpdateServer;
    private readonly IFileService _fileService;
    private readonly IModuleStorage _moduleStorage;
    private readonly IClientNotificationService _notifications;

    [ObservableProperty]
    private string _codePreview = string.Empty;

    [ObservableProperty]
    private string _message = "API 更新";
    private IList<string> _selectedApiCodes = new List<string>();

    [ObservableProperty]
    private string _sourceCodePath = SsamcEnvironment.SourceCodePath;

    [ObservableProperty]
    private string _sourceCodePreview = string.Empty;

    [ObservableProperty]
    private string _strSelectApiCodes = string.Empty;

    [ObservableProperty]
    private string _strUpdateApis = string.Empty;

    public ApiUpdateViewModel(
        IApiUpdateServer apiUpdateServer,
        IFileService fileService,
        IModuleStorage moduleStorage,
        IClientNotificationService notifications)
    {
        _apiUpdateServer = apiUpdateServer;
        _fileService = fileService;
        _moduleStorage = moduleStorage;
        _notifications = notifications;
    }

    public IList<string> SelectApiCodes
    {
        get => _selectedApiCodes;
        set => SetProperty(ref _selectedApiCodes, value);
    }

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
        return RunWithLoadingAsync(async cancellationToken =>
        {
            ValidateSourcePath();

            var allApiSourceInfos = await Task.Run(
                                        () => _apiUpdateServer.GetAllApiSourceInfos(SourceCodePath),
                                        cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            await SaveSettingsAsync(cancellationToken);
        }, HandleOperationError);
    }

    [RelayCommand]
    private Task Preview()
    {
        return RunWithLoadingAsync(async cancellationToken =>
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
        }, HandleOperationError);
    }

    [RelayCommand]
    private Task Update(string? environmentKey)
    {
        return RunWithLoadingAsync(async cancellationToken =>
        {
            ValidateSourcePath();

            var updateApiCodes = (StrUpdateApis ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (updateApiCodes.Count == 0)
            {
                ShowWarning("请输入要更新的接口");
                return;
            }

            if (string.IsNullOrWhiteSpace(environmentKey))
            {
                ShowWarning("未指定数据库目标环境。");
                return;
            }

            var connectionString = SsamcEnvironment.GetDatabaseConnection(environmentKey);
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
        }, HandleOperationError);
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

    private void HandleOperationError(Exception exception)
    {
        if (exception is SsamcConfigurationException or DirectoryNotFoundException)
        {
            ShowWarning(exception.Message);
            return;
        }

        ShowError(exception.Message);
    }

    private void ValidateSourcePath()
    {
        if (string.IsNullOrWhiteSpace(SourceCodePath))
        {
            throw new SsamcConfigurationException("请先配置源代码目录。");
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

    #region Nested type: ApiUpdateSettings
    private sealed class ApiUpdateSettings
    {
        public string? SourceCodePath { get; set; }

        public string? StrSelectApiCodes { get; set; }
    }
    #endregion
}
