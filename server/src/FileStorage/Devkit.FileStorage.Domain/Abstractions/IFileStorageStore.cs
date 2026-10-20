using Devkit.DomainCommons.Entities;
namespace Devkit.FileStorage.Domain.Abstractions;
public interface IFileStorageStore
{
    IQueryable<T> Query<T>() where T : AuditableEntity;
    void Add<T>(T entity) where T : AuditableEntity;
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task<T> TransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
}
