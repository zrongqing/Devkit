using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.UI.Mvvm;
using Devkit.Services.Interfaces.Notifications;
using Barcode2.Configuration;

namespace Barcode2.ViewModels;

public partial class Barcode2SettingsViewModel : LoadingViewModelBase
{
    private readonly IBarcode2ConfigurationService _configuration;
    private readonly IBarcode2ConnectionTester _connectionTester;
    private readonly IClientNotificationService _notifications;

    [ObservableProperty]
    private ObservableCollection<EnvironmentEditor> _environments = [];

    [ObservableProperty]
    private ObservableCollection<ShareEditor> _shareTargets = [];

    [ObservableProperty]
    private string _sourceCodePath = string.Empty;

    [ObservableProperty]
    private string _webappSourcePath = string.Empty;

    public Barcode2SettingsViewModel(
        IBarcode2ConfigurationService configuration,
        IBarcode2ConnectionTester connectionTester,
        IClientNotificationService notifications)
    {
        _configuration = configuration;
        _connectionTester = connectionTester;
        _notifications = notifications;
    }

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            HandleError(exception);
        }
    }

    [RelayCommand]
    private Task Reload()
    {
        return RunWithLoadingAsync(LoadAsync, HandleError);
    }

    [RelayCommand]
    private Task Save()
    {
        return RunWithLoadingAsync(async cancellationToken =>
        {
            var settings = await _configuration.GetAsync(cancellationToken);
            settings.SourceCodePath = SourceCodePath;
            settings.WebappSourcePath = WebappSourcePath;
            settings.Environments = Environments.Select(editor => editor.ToSettings()).ToList();
            settings.ShareTargets = ShareTargets.Select(editor => editor.ToSettings()).ToList();
            await _configuration.SaveAsync(settings, cancellationToken);
            Show(NotificationLevel.Info, "Barcode2 配置已保存。");
        }, HandleError);
    }

    [RelayCommand]
    private Task TestEnvironment(EnvironmentEditor? editor)
    {
        return RunWithLoadingAsync(async cancellationToken =>
        {
            if (editor is null)
            {
                throw new Barcode2ConfigurationException("请选择要检测的环境。");
            }

            await _connectionTester.TestEnvironmentAsync(editor.ToSettings(), cancellationToken);
            Show(NotificationLevel.Info, $"{editor.DisplayName}服务器及数据库连接正常。");
        }, HandleError);
    }

    [RelayCommand]
    private Task TestShare(ShareEditor? editor)
    {
        return RunWithLoadingAsync(async cancellationToken =>
        {
            if (editor is null)
            {
                throw new Barcode2ConfigurationException("请选择要检测的 Webapp 发布目标。");
            }

            await _connectionTester.TestShareAsync(editor.ToSettings(), cancellationToken);
            Show(NotificationLevel.Info, $"{editor.DisplayName}发布目录连接正常。");
        }, HandleError);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await _configuration.GetAsync(cancellationToken);
        SourceCodePath = settings.SourceCodePath;
        WebappSourcePath = settings.WebappSourcePath;
        Environments = new ObservableCollection<EnvironmentEditor>(
            settings.Environments.Select(EnvironmentEditor.FromSettings));
        ShareTargets = new ObservableCollection<ShareEditor>(
            settings.ShareTargets.Select(ShareEditor.FromSettings));
    }

    private void HandleError(Exception exception)
    {
        Show(
            exception is Barcode2ConfigurationException ? NotificationLevel.Warning : NotificationLevel.Error,
            exception.Message);
    }

    private void Show(NotificationLevel level, string message)
    {
        _notifications.Show(new NotificationRequest
        {
            Message = message,
            Level = level
        });
    }

    public partial class EnvironmentEditor : ObservableObject
    {
        public string Key { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;

        [ObservableProperty]
        private string _databaseDataSource = string.Empty;

        [ObservableProperty]
        private string _databasePassword = string.Empty;

        [ObservableProperty]
        private string _databaseUsername = string.Empty;

        [ObservableProperty]
        private string _pageBaseAddress = string.Empty;

        public static EnvironmentEditor FromSettings(Barcode2EnvironmentSettings settings) => new()
        {
            Key = settings.Key,
            DisplayName = settings.DisplayName,
            DatabaseDataSource = settings.DatabaseDataSource,
            DatabaseUsername = settings.DatabaseUsername,
            DatabasePassword = settings.DatabasePassword,
            PageBaseAddress = settings.PageBaseAddress
        };

        public Barcode2EnvironmentSettings ToSettings() => new()
        {
            Key = Key,
            DisplayName = DisplayName,
            DatabaseDataSource = DatabaseDataSource,
            DatabaseUsername = DatabaseUsername,
            DatabasePassword = DatabasePassword,
            PageBaseAddress = PageBaseAddress
        };
    }

    public partial class ShareEditor : ObservableObject
    {
        public string Id { get; init; } = string.Empty;
        public string TargetKey { get; init; } = string.Empty;
        public int Order { get; init; }
        public string DisplayName { get; init; } = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _root = string.Empty;

        [ObservableProperty]
        private string _username = string.Empty;

        public static ShareEditor FromSettings(Barcode2ShareSettings settings) => new()
        {
            Id = settings.Id,
            TargetKey = settings.TargetKey,
            Order = settings.Order,
            DisplayName = settings.DisplayName,
            Root = settings.Root,
            Username = settings.Username,
            Password = settings.Password
        };

        public Barcode2ShareSettings ToSettings() => new()
        {
            Id = Id,
            TargetKey = TargetKey,
            Order = Order,
            DisplayName = DisplayName,
            Root = Root,
            Username = Username,
            Password = Password
        };
    }
}
