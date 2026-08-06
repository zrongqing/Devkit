namespace Devkit.Core.UI.Mvvm;

/// <summary>
/// Optional base class for view models that run mutually exclusive operations
/// with delayed loading feedback.
/// </summary>
public abstract class LoadingViewModelBase : ViewModelBase
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public DelayedLoadingState PageLoading { get; } = new();

    /// <summary>
    /// Runs an operation when no other page operation is active. Calls made
    /// while another operation is running are ignored.
    /// </summary>
    protected async Task RunWithLoadingAsync(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!await _operationGate.WaitAsync(0, LifetimeCancellationToken))
        {
            return;
        }

        try
        {
            await PageLoading.RunAsync(operation, LifetimeCancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }
}
