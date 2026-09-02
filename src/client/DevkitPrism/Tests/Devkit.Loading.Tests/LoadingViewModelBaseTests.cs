using Devkit.Core.UI.Mvvm;
using Xunit;

namespace Devkit.Loading.Tests;

public class LoadingViewModelBaseTests
{
    [Fact]
    public async Task Failure_is_reported_and_loading_state_is_reset()
    {
        var viewModel = new TestLoadingViewModel();

        await viewModel.RunAsync(_ =>
            Task.FromException(new InvalidOperationException("server unavailable")));

        Assert.Equal("server unavailable", viewModel.LastError?.Message);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
    }

    [Fact]
    public async Task Lifetime_cancellation_is_not_reported_as_an_error()
    {
        var viewModel = new TestLoadingViewModel();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var operation = viewModel.RunAsync(async cancellationToken =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        await started.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        viewModel.Destroy();
        await operation;

        Assert.Null(viewModel.LastError);
        Assert.False(viewModel.PageLoading.IsBusy);
        Assert.False(viewModel.PageLoading.IsVisible);
    }

    private sealed class TestLoadingViewModel : LoadingViewModelBase
    {
        public Exception? LastError { get; private set; }

        public Task RunAsync(Func<CancellationToken, Task> operation) =>
            RunWithLoadingAsync(operation, exception => LastError = exception);
    }
}
