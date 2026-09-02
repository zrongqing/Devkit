using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.UI.Mvvm;
using Devkit.Services.Interfaces.Notifications;
using Barcode2.Configuration;
using Barcode2.Servers;

namespace Barcode2.ViewModels;

public partial class ApiUpdateViewModel : LoadingViewModelBase
{
    private readonly IApiUpdateServer _apiUpdateServer;
    private readonly IBarcode2ConfigurationService _configuration;
    private readonly IClientNotificationService _notifications;

    [ObservableProperty]
    private string _codePreview = string.Empty;

    [ObservableProperty]
    private string _message = "API 更新";
    private IList<string> _selectedApiCodes = new List<string>();

    [ObservableProperty]
    private string _sourceCodePath = string.Empty;

    [ObservableProperty]
    private string _sourceCodePreview = string.Empty;

    [ObservableProperty]
    private string _strSelectApiCodes = string.Empty;

    [ObservableProperty]
    private string _strSelectApiName = string.Empty;

    [ObservableProperty]
    private string _strUpdateApis = string.Empty;

    [ObservableProperty]
    private string _strUpdateApiNames = string.Empty;

    public ApiUpdateViewModel(
        IApiUpdateServer apiUpdateServer,
        IBarcode2ConfigurationService configuration,
        IClientNotificationService notifications)
    {
        _apiUpdateServer = apiUpdateServer;
        _configuration = configuration;
        _notifications = notifications;
    }

    public IList<string> SelectApiCodes
    {
        get => _selectedApiCodes;
        set => SetProperty(ref _selectedApiCodes, value);
    }

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _configuration.GetAsync(cancellationToken);
            SourceCodePath = settings.SourceCodePath;
            StrSelectApiCodes = settings.SelectedApiCode;
            StrSelectApiName = settings.SelectedApiName;
        }
        catch (Exception exception)
        {
            HandleOperationError(exception);
        }
    }

    [RelayCommand]
    private Task ScanSourceCode()
    {
        return RunWithLoadingAsync(async cancellationToken =>
        {
            await RefreshSourcePathAsync(cancellationToken);
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
            await RefreshSourcePathAsync(cancellationToken);
            ValidateSourcePath();

            var apiCode = StrSelectApiCodes?.Trim();
            var apiName = StrSelectApiName?.Trim();
            if (string.IsNullOrWhiteSpace(apiCode) && string.IsNullOrWhiteSpace(apiName))
            {
                throw new InvalidOperationException("请输入要预览的 ApiCode 或 ApiName。");
            }

            var previews = await Task.Run(() =>
            {
                var executionSource = !string.IsNullOrWhiteSpace(apiCode)
                                          ? _apiUpdateServer.GetExecutionSourceCodeByApiCode(
                                              SourceCodePath,
                                              apiCode)
                                          : _apiUpdateServer.GetExecutionSourceCodeByApiName(
                                              SourceCodePath,
                                              apiName!);
                var extendSource = !string.IsNullOrWhiteSpace(apiCode)
                                       ? _apiUpdateServer.GetSourceCodeByApiCode(
                                           SourceCodePath,
                                           apiCode)
                                       : _apiUpdateServer.GetSourceCodeByApiName(
                                           SourceCodePath,
                                           apiName!);
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
            await RefreshSourcePathAsync(cancellationToken);
            ValidateSourcePath();

            var updateApiCodes = ParseIdentifiers(StrUpdateApis);
            var lookupKind = updateApiCodes.Count > 0 ? ApiLookupKind.Code : ApiLookupKind.Name;
            var updateIdentifiers = updateApiCodes.Count > 0
                                        ? updateApiCodes
                                        : ParseIdentifiers(StrUpdateApiNames);
            if (updateIdentifiers.Count == 0)
            {
                ShowWarning("请输入要更新的接口");
                return;
            }

            if (string.IsNullOrWhiteSpace(environmentKey))
            {
                ShowWarning("未指定数据库目标环境。");
                return;
            }

            var connectionString = await _configuration.GetDatabaseConnectionStringAsync(
                environmentKey,
                cancellationToken);
            var results = await Task.Run(() => UpdateApis(
                              updateIdentifiers,
                              lookupKind,
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
        IEnumerable<string> updateIdentifiers,
        ApiLookupKind lookupKind,
        string connectionString,
        CancellationToken cancellationToken)
    {
        var apiInfos = _apiUpdateServer.GetAllApiSourceInfos(SourceCodePath);
        var updates = new List<ApiExtendUpdateRequest>();

        foreach (var identifier in updateIdentifiers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceCode = lookupKind == ApiLookupKind.Code
                                 ? _apiUpdateServer.GetSourceCodeByApiCode(apiInfos, identifier)
                                 : _apiUpdateServer.GetSourceCodeByApiName(apiInfos, identifier);
            updates.Add(new ApiExtendUpdateRequest(lookupKind, identifier, sourceCode));
        }

        _apiUpdateServer.UpdateExtendBatch(updates, connectionString);
        return updates.Select(update => $"{update.Identifier}，更新成功").ToList();
    }

    private static List<string> ParseIdentifiers(string? value)
    {
        return (value ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private void HandleOperationError(Exception exception)
    {
        if (exception is Barcode2ConfigurationException or DirectoryNotFoundException)
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
            throw new Barcode2ConfigurationException("请先配置源代码目录。");
        }

        if (!Directory.Exists(SourceCodePath))
        {
            throw new DirectoryNotFoundException($"源代码目录不存在：{SourceCodePath}");
        }
    }

    private Task SaveSettingsAsync(CancellationToken cancellationToken)
    {
        return SaveSettingsCoreAsync(cancellationToken);
    }

    private async Task SaveSettingsCoreAsync(CancellationToken cancellationToken)
    {
        var settings = await _configuration.GetAsync(cancellationToken);
        settings.SelectedApiCode = StrSelectApiCodes ?? string.Empty;
        settings.SelectedApiName = StrSelectApiName ?? string.Empty;
        await _configuration.SaveAsync(settings, cancellationToken);
    }

    private async Task RefreshSourcePathAsync(CancellationToken cancellationToken)
    {
        SourceCodePath = (await _configuration.GetAsync(cancellationToken)).SourceCodePath;
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
