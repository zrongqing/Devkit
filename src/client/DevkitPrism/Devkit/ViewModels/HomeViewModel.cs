using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.UI.Mvvm;
using Devkit.Services;

namespace Devkit.ViewModels;

public partial class HomeViewModel(ISystemInfoClient systemInfoClient) : ViewModelBase
{
    [ObservableProperty]
    private string _statusMessage = "尚未读取服务器状态。";

    [RelayCommand]
    private async Task RefreshSystemInfoAsync()
    {
        StatusMessage = "正在读取服务器状态…";
        try
        {
            var info = await systemInfoClient.GetInfoAsync();
            StatusMessage = $"{info.ServiceName} | {info.Version} | {info.Environment} | {info.ServerTime.LocalDateTime:G}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"服务器不可用：{exception.Message}";
        }
    }
}
