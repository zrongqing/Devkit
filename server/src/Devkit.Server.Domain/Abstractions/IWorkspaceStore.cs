using System.Linq.Expressions;
using Devkit.Server.Domain.Common;

namespace Devkit.Server.Domain.Abstractions;

public interface IWorkspaceStore
{
    Task<T?> FindAsync<T>(Guid id, CancellationToken ct = default) where T : AuditableEntity;
    Task<List<T>> ListAsync<T>(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) where T : AuditableEntity;
    void Add<T>(T entity) where T : AuditableEntity;
    Task SaveAsync(CancellationToken ct = default);
    Task RefreshAsync<T>(T entity, CancellationToken ct = default) where T : AuditableEntity;
    Task<T> TransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}
