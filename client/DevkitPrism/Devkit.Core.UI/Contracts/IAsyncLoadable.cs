namespace Devkit.Core.UI.Contracts;

/// <summary>
/// Provides asynchronous first-load initialization for tab content.
/// Implementations should keep constructors lightweight and perform expensive
/// I/O or data preparation in <see cref="InitializeAsync"/>.
/// </summary>
public interface IAsyncLoadable
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
