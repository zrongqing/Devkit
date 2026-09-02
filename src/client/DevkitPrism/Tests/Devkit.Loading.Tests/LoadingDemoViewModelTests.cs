using Devkit.Modules.Demo.ViewModels;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class LoadingDemoViewModelTests
{
    [Fact]
    public async Task Fast_success_reports_success_and_resets_loading()
    {
        var viewModel = new LoadingDemoViewModel();

        await viewModel.FastSuccessCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasError);
        Assert.Contains("快速操作成功", viewModel.StatusMessage);
        Assert.Equal(1, viewModel.OperationCount);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
    }

    [Fact]
    public async Task Server_failure_reports_error_and_resets_loading()
    {
        var viewModel = new LoadingDemoViewModel();

        await viewModel.ServerFailureCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("HTTP 500", viewModel.StatusMessage);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
    }

    [Fact]
    public async Task Missing_configuration_reports_error_and_resets_loading()
    {
        var viewModel = new LoadingDemoViewModel();

        await viewModel.MissingConfigurationCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("配置缺失", viewModel.StatusMessage);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
    }
}
