using Devkit.Core.UI.Contracts;
using Devkit.Core.UI.Mvvm;
using Xunit;

namespace Devkit.Loading.Tests;

public class ViewModelBaseTests
{
    [Fact]
    public async Task View_model_without_custom_initialization_completes_immediately()
    {
        var viewModel = new EmptyViewModel();

        await ((IAsyncLoadable)viewModel).InitializeAsync(CancellationToken.None);

        Assert.Equal(0, viewModel.DestroyCount);
    }

    [Fact]
    public async Task Destroy_cancels_initialization_and_runs_cleanup_once()
    {
        var viewModel = new CancelableViewModel();
        var initialization = viewModel.InitializeAsync(CancellationToken.None);
        await viewModel.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        viewModel.Destroy();
        viewModel.Destroy();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization);
        Assert.Equal(1, viewModel.DestroyCount);
    }

    private sealed class EmptyViewModel : ViewModelBase
    {
        public int DestroyCount { get; private set; }

        protected override void OnDestroy()
        {
            DestroyCount++;
        }
    }

    private sealed class CancelableViewModel : ViewModelBase
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DestroyCount { get; private set; }

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        protected override void OnDestroy()
        {
            DestroyCount++;
        }
    }
}
