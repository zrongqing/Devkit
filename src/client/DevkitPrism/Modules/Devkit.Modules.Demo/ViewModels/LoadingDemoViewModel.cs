using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.UI.Mvvm;

namespace Devkit.Modules.Demo.ViewModels;

public partial class LoadingDemoViewModel : LoadingViewModelBase
{
    [ObservableProperty]
    private string _statusMessage = "请选择一种场景开始测试。";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private int _operationCount;

    [RelayCommand]
    private Task FastSuccess() => RunDemoOperationAsync(
        async cancellationToken =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            SetSuccess("快速操作成功：耗时不足 1 秒，不应显示加载遮罩。");
        });

    [RelayCommand]
    private Task SlowSuccess() => RunDemoOperationAsync(
        async cancellationToken =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            SetSuccess("慢速操作成功：加载遮罩已显示并自动关闭。");
        });

    [RelayCommand]
    private Task ServerFailure() => RunDemoOperationAsync(
        async cancellationToken =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            throw new InvalidOperationException("模拟服务器错误：HTTP 500。");
        });

    [RelayCommand]
    private Task MissingConfiguration() => RunDemoOperationAsync(
        _ => throw new InvalidOperationException("模拟配置缺失：请先配置服务地址。"));

    private Task RunDemoOperationAsync(Func<CancellationToken, Task> operation)
    {
        OperationCount++;
        HasError = false;
        StatusMessage = "操作执行中…";
        return RunWithLoadingAsync(operation, HandleOperationError);
    }

    private void SetSuccess(string message)
    {
        HasError = false;
        StatusMessage = message;
    }

    private void HandleOperationError(Exception exception)
    {
        HasError = true;
        StatusMessage = exception.Message;
    }
}
