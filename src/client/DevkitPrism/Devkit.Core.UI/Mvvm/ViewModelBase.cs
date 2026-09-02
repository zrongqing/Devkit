using CommunityToolkit.Mvvm.ComponentModel;
using Devkit.Core.UI.Contracts;

namespace Devkit.Core.UI.Mvvm;

/// <summary>
/// Base class for view models with a shared asynchronous initialization and
/// destruction lifetime.
/// </summary>
public abstract class ViewModelBase : ObservableObject, IDestructible, IAsyncLoadable
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private int _isDestroyed;

    /// <summary>
    /// Gets a token that is canceled when <see cref="Destroy"/> is called.
    /// </summary>
    protected CancellationToken LifetimeCancellationToken => _lifetimeCancellation.Token;

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            LifetimeCancellationToken);
        linkedCancellation.Token.ThrowIfCancellationRequested();
        await OnInitializeAsync(linkedCancellation.Token);
    }

    /// <summary>
    /// Performs view-model-specific asynchronous initialization. View models
    /// without initialization requirements use the default completed task.
    /// </summary>
    protected virtual Task OnInitializeAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public virtual void Destroy()
    {
        if (Interlocked.Exchange(ref _isDestroyed, 1) != 0)
        {
            return;
        }

        _lifetimeCancellation.Cancel();
        OnDestroy();
    }

    /// <summary>
    /// Releases view-model-specific resources after the shared lifetime has
    /// been canceled.
    /// </summary>
    protected virtual void OnDestroy()
    {
    }
}
