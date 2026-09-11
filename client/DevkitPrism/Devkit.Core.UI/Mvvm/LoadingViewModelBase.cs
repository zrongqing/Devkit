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
    /// Runs a page operation with loading feedback and reports non-lifetime
    /// failures through the supplied handler.
    /// </summary>
    protected async Task RunWithLoadingAsync(
        Func<CancellationToken, Task> operation,
        Action<Exception> errorHandler)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(errorHandler);

        try
        {
            await RunWithLoadingAsync(operation);
        }
        catch (OperationCanceledException) when (LifetimeCancellationToken.IsCancellationRequested)
        {
            // Closing the view ends its current operation without surfacing an error.
        }
        catch (Exception exception)
        {
            errorHandler(exception);
        }
    }

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
