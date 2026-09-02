using CommunityToolkit.Mvvm.ComponentModel;

namespace Devkit.Core.UI.Mvvm;

/// <summary>
/// Tracks one or more operations and only exposes the visual loading state
/// after a configurable delay.
/// </summary>
public partial class DelayedLoadingState : ObservableObject
{
    public static readonly TimeSpan DefaultShowDelay = TimeSpan.FromSeconds(1);

    private readonly object _syncRoot = new();
    private readonly TimeSpan _showDelay;
    private int _activeOperationCount;
    private int _visibleOperationCount;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isVisible;

    public DelayedLoadingState()
        : this(DefaultShowDelay)
    {
    }

    public DelayedLoadingState(TimeSpan showDelay)
    {
        if (showDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(showDelay));
        }

        _showDelay = showDelay;
    }

    public TimeSpan ShowDelay => _showDelay;

    public async Task RunAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        BeginOperation();
        var becameVisible = false;

        try
        {
            var operationTask = InvokeOperationAsync(operation, cancellationToken);
            var delayTask = Task.Delay(_showDelay);
            var completedTask = await Task.WhenAny(operationTask, delayTask);

            if (completedTask == delayTask && !operationTask.IsCompleted)
            {
                becameVisible = true;
                MarkOperationVisible();
            }

            await operationTask;
        }
        finally
        {
            EndOperation(becameVisible);
        }
    }

    private static async Task InvokeOperationAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await operation(cancellationToken);
    }

    private void BeginOperation()
    {
        bool isBusy;
        lock (_syncRoot)
        {
            _activeOperationCount++;
            isBusy = _activeOperationCount > 0;
        }

        IsBusy = isBusy;
    }

    private void MarkOperationVisible()
    {
        bool isVisible;
        lock (_syncRoot)
        {
            _visibleOperationCount++;
            isVisible = _visibleOperationCount > 0;
        }

        IsVisible = isVisible;
    }

    private void EndOperation(bool wasVisible)
    {
        bool isBusy;
        bool isVisible;

        lock (_syncRoot)
        {
            if (wasVisible)
            {
                _visibleOperationCount--;
            }

            _activeOperationCount--;
            isBusy = _activeOperationCount > 0;
            isVisible = _visibleOperationCount > 0;
        }

        IsVisible = isVisible;
        IsBusy = isBusy;
    }
}
