using System.Linq.Expressions;
using Devkit.DomainCommons.Querying;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Infrastructure.Persistence;

public abstract class PagedDbContext(DbContextOptions options) : DbContext(options), IPagedQueryStore
{
    public Task<PageResult<T>> PageAsync<T>(PageQuery query, Expression<Func<T, bool>> scope, CancellationToken ct = default) where T : class
    {
        PageQueries.Validate(query);
        var keys = Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties
            ?? throw new QueryValidationException("此表没有可用的分页主键。");
        return QueryPageAsync(Set<T>().AsNoTracking().Where(scope), query, ct, keys.Select(k => k.Name).ToArray());
    }

    public static async Task<PageResult<T>> QueryPageAsync<T>(IQueryable<T> source, PageQuery query, CancellationToken ct = default, IReadOnlyList<string>? uniqueFields = null)
    {
        var filtered = PageQueries.Filter(source, query);
        var ordered = PageQueries.Order(filtered, query, uniqueFields);
        var total = await filtered.CountAsync(ct);
        var items = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }
}
