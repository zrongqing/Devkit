using Barcode2.Models;
using Devkit.Services.Interfaces.Logging;

namespace Barcode2.Servers;

public sealed class CachedMenuTreeDataSource(
    OracleMenuTreeDataSource remoteSource,
    IMenuTreeCache cache,
    IClientLogger logger) : ICachedMenuTreeDataSource
{
    public Task<IReadOnlyList<MenuTreeItem>> GetCachedMenuTreeAsync(
        string environmentKey,
        CancellationToken cancellationToken) =>
        cache.ReadAsync(environmentKey, cancellationToken);

    public async Task<IReadOnlyList<MenuTreeItem>> GetMenuTreeAsync(
        string environmentKey,
        CancellationToken cancellationToken)
    {
        var items = await remoteSource
            .GetMenuTreeAsync(environmentKey, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await cache
                .WriteAsync(environmentKey, items, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.Warning(exception, "The menu search result could not be cached.");
        }

        return items;
    }
}
