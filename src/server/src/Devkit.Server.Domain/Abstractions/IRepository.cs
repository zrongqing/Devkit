namespace Devkit.Server.Domain.Abstractions;

/// <summary>Persistence seam. Infrastructure supplies a concrete store in a later iteration.</summary>
public interface IRepository<TAggregate, in TId>
    where TAggregate : class
{
    Task<TAggregate?> FindAsync(TId id, CancellationToken cancellationToken = default);
    Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}
