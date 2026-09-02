using Devkit.Core.UI.Mvvm;
using Xunit;

namespace Devkit.Loading.Tests;

public class DelayedLoadingStateTests
{
    [Fact]
    public async Task Fast_operation_never_becomes_visible()
    {
        var state = new DelayedLoadingState(TimeSpan.FromMilliseconds(100));

        await state.RunAsync(_ => Task.Delay(10), TestContext.Current.CancellationToken);

        Assert.False(state.IsBusy);
        Assert.False(state.IsVisible);
    }

    [Fact]
    public async Task Slow_operation_becomes_visible_and_resets()
    {
        var state = new DelayedLoadingState(TimeSpan.FromMilliseconds(20));
        var completion = NewCompletion();

        var operation = state.RunAsync(_ => completion.Task, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => state.IsVisible);

        Assert.True(state.IsBusy);
        completion.SetResult();
        await operation;

        Assert.False(state.IsBusy);
        Assert.False(state.IsVisible);
    }

    [Fact]
    public async Task Failure_and_cancellation_always_reset_state()
    {
        var state = new DelayedLoadingState(TimeSpan.Zero);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            state.RunAsync(_ => Task.FromException(new InvalidOperationException("failed")), TestContext.Current.CancellationToken));
        Assert.False(state.IsBusy);
        Assert.False(state.IsVisible);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            state.RunAsync(_ => Task.CompletedTask, cancellation.Token));
        Assert.False(state.IsBusy);
        Assert.False(state.IsVisible);
    }

    [Fact]
    public async Task Visible_state_remains_until_all_slow_operations_finish()
    {
        var state = new DelayedLoadingState(TimeSpan.FromMilliseconds(20));
        var firstCompletion = NewCompletion();
        var secondCompletion = NewCompletion();

        var first = state.RunAsync(_ => firstCompletion.Task, TestContext.Current.CancellationToken);
        var second = state.RunAsync(_ => secondCompletion.Task, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => state.IsVisible);

        firstCompletion.SetResult();
        await first;
        Assert.True(state.IsBusy);
        Assert.True(state.IsVisible);

        secondCompletion.SetResult();
        await second;
        Assert.False(state.IsBusy);
        Assert.False(state.IsVisible);
    }

    private static TaskCompletionSource NewCompletion() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(5, timeout.Token);
        }
    }
}
