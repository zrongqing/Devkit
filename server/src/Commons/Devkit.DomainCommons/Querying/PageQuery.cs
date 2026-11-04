using System.Linq.Expressions;

namespace Devkit.DomainCommons.Querying;

public sealed record FieldFilter(string Field, string Value, string Operator = "contains");
public sealed record FieldSort(string Field, string Direction = "asc");
public sealed record PageQuery(int Page = 1, int PageSize = 20, FieldFilter[]? Filters = null, FieldSort[]? Sorts = null);
public sealed record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed class QueryValidationException(string message) : Exception(message);

/// <summary>Internal, scoped access for every mapped table. Never expose entity types from client input.</summary>
public interface IPagedQueryStore
{
    Task<PageResult<T>> PageAsync<T>(PageQuery query, Expression<Func<T, bool>> scope, CancellationToken ct = default) where T : class;
}
