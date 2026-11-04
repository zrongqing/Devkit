using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Devkit.DomainCommons.Querying;

/// <summary>Shared query validation, field resolution and stable ordering, independent of persistence.</summary>
public static class PageQueries
{
    public static void Validate(PageQuery query)
    {
        if (query is null) throw new QueryValidationException("查询条件不能为空。");
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw new QueryValidationException("页码必须为正数，每页 1 到 100 条。");
        if (query.Filters?.Length > 32 || query.Sorts?.Length > 8)
            throw new QueryValidationException("最多支持 32 个筛选条件和 8 个排序字段。");
        if (query.Filters?.Any(f => f is null || string.IsNullOrWhiteSpace(f.Field) || f.Value is null || f.Value.Length > 512 ||
            f.Operator is not ("contains" or "eq" or "gte" or "lte")) == true ||
            query.Sorts?.Any(s => s is null || string.IsNullOrWhiteSpace(s.Field) || s.Direction is not ("asc" or "desc")) == true)
            throw new QueryValidationException("筛选字段、值或排序方向无效。");
        if (query.Sorts?.Select(s => s.Field).Distinct(StringComparer.OrdinalIgnoreCase).Count() != query.Sorts?.Length)
            throw new QueryValidationException("排序字段不能重复。");
    }

    private static PropertyInfo Property(Type type, string name) => type.GetProperties().FirstOrDefault(p =>
        p.GetIndexParameters().Length == 0 && p.GetMethod?.IsPublic == true && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new QueryValidationException($"不支持查询字段：{name}。");

    private static Expression Field(Expression row, string field)
    {
        if (field.Length > 120 || field.Split('.').Length > 3) throw new QueryValidationException("查询字段无效。");
        Expression value = row;
        foreach (var part in field.Split('.')) value = Expression.Property(value, Property(value.Type, part));
        return value;
    }

    private static object Parse(string value, Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        try
        {
            if (type == typeof(Guid)) return Guid.Parse(value);
            if (type == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (type.IsEnum) return Enum.Parse(type, value, true);
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException or ArgumentException)
        { throw new QueryValidationException($"字段值不能转换为 {type.Name}。"); }
    }

    public static IQueryable<T> Filter<T>(IQueryable<T> source, PageQuery query)
    {
        Validate(query);
        foreach (var filter in query.Filters ?? [])
        {
            var row = Expression.Parameter(typeof(T), "row");
            var field = Field(row, filter.Field);
            Expression predicate;
            if (field.Type == typeof(string))
            {
                if (filter.Operator is "gte" or "lte") throw new QueryValidationException("文本仅支持 contains 和 eq。");
                var lowered = Expression.Call(field, nameof(string.ToLower), Type.EmptyTypes);
                var value = Expression.Constant(filter.Value.ToLowerInvariant());
                predicate = filter.Operator == "eq" ? Expression.Equal(lowered, value)
                    : Expression.Call(lowered, nameof(string.Contains), Type.EmptyTypes, value);
                predicate = Expression.AndAlso(Expression.NotEqual(field, Expression.Constant(null, typeof(string))), predicate);
            }
            else
            {
                var type = Nullable.GetUnderlyingType(field.Type) ?? field.Type;
                if (!(type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset)))
                    throw new QueryValidationException($"字段 {filter.Field} 不支持标量筛选。");
                var value = Expression.Convert(Expression.Constant(Parse(filter.Value, type)), field.Type);
                try
                {
                    predicate = filter.Operator switch
                    {
                        "gte" => Expression.GreaterThanOrEqual(field, value),
                        "lte" => Expression.LessThanOrEqual(field, value),
                        _ => Expression.Equal(field, value),
                    };
                }
                catch (InvalidOperationException) { throw new QueryValidationException("此字段不支持范围筛选。"); }
            }
            source = source.Where(Expression.Lambda<Func<T, bool>>(predicate, row));
        }
        return source;
    }

    public static IQueryable<T> Order<T>(IQueryable<T> source, PageQuery query, IReadOnlyList<string>? uniqueFields = null)
    {
        Validate(query);
        var sorts = (query.Sorts ?? []).ToList();
        var key = typeof(T).GetProperties().FirstOrDefault(p => p.Name == "Id") ?? typeof(T).GetProperties().FirstOrDefault(p => p.Name == "MenuCode");
        foreach (var name in uniqueFields ?? (key is null ? [] : new[] { key.Name }))
            if (!sorts.Any(s => s.Field.Equals(name, StringComparison.OrdinalIgnoreCase))) sorts.Add(new(name));
        if (sorts.Count == 0) throw new QueryValidationException("分页查询必须声明唯一键或稳定排序。");
        var first = true;
        foreach (var sort in sorts)
        {
            var row = Expression.Parameter(typeof(T), "row");
            var field = Field(row, sort.Field);
            if (field.Type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(field.Type)) throw new QueryValidationException("集合字段请使用查询投影排序。");
            var method = first ? "OrderBy" : "ThenBy";
            if (sort.Direction == "desc") method += "Descending";
            source = source.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), method, [typeof(T), field.Type], source.Expression, Expression.Quote(Expression.Lambda(field, row))));
            first = false;
        }
        return source;
    }

    // Authorized DTO snapshots and derived views share the same contract; custom fields are server-owned.
    public static PageResult<T> Snapshot<T>(IEnumerable<T> source, PageQuery query, IReadOnlyDictionary<string, Func<T, object?>>? fields = null)
    {
        var rows = SelectSnapshot(source, query, fields);
        return new(rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray(), rows.Length, query.Page, query.PageSize);
    }
    public static T[] SelectSnapshot<T>(IEnumerable<T> source, PageQuery query, IReadOnlyDictionary<string, Func<T, object?>>? fields = null)
    {
        Validate(query);
        Func<T, object?> Resolve(string name)
        {
            if (fields?.TryGetValue(name, out var selector) == true) return selector;
            var parts = name.Split('.');
            var type = typeof(T);
            var properties = new List<PropertyInfo>();
            if (name.Length > 120 || parts.Length > 3) throw new QueryValidationException("查询字段无效。");
            foreach (var part in parts) { var p = Property(type, part); properties.Add(p); type = p.PropertyType; }
            return row => { object? value = row; foreach (var p in properties) value = value is null ? null : p.GetValue(value); return value; };
        }
        var rows = source;
        foreach (var filter in query.Filters ?? [])
        {
            var get = Resolve(filter.Field);
            rows = rows.Where(row => Match(get(row), filter));
        }
        var materialized = rows.ToArray();
        IOrderedEnumerable<T>? ordered = null;
        var sorts = (query.Sorts ?? []).ToList();
        var key = typeof(T).GetProperties().FirstOrDefault(p => p.Name is "Id" or "MenuCode" or "Key");
        if (key is not null && !sorts.Any(s => s.Field.Equals(key.Name, StringComparison.OrdinalIgnoreCase))) sorts.Add(new(key.Name));
        foreach (var sort in sorts)
        {
            var get = Resolve(sort.Field);
            var comparer = Comparer<object?>.Create(Compare);
            ordered = ordered is null ? sort.Direction == "desc" ? materialized.OrderByDescending(get, comparer) : materialized.OrderBy(get, comparer)
                : sort.Direction == "desc" ? ordered.ThenByDescending(get, comparer) : ordered.ThenBy(get, comparer);
        }
        return (ordered ?? materialized.AsEnumerable()).ToArray();
    }
    private static string Text(object? value) => value is null ? "" : value is string s ? s : value is IEnumerable list ? string.Join("、", list.Cast<object?>().Select(Text)) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    private static int Compare(object? left, object? right) => left is null ? right is null ? 0 : -1 : right is null ? 1
        : left is string l && right is string r ? StringComparer.OrdinalIgnoreCase.Compare(l, r)
        : left is IComparable comparable && left.GetType() == right.GetType() ? comparable.CompareTo(right) : StringComparer.OrdinalIgnoreCase.Compare(Text(left), Text(right));
    private static bool Match(object? value, FieldFilter filter)
    {
        if (filter.Operator == "contains" && (value is null || value is string || value is IEnumerable))
            return Text(value).Contains(filter.Value, StringComparison.OrdinalIgnoreCase);
        if (value is null) return false;
        var comparison = Compare(value, value is string ? filter.Value : Parse(filter.Value, value.GetType()));
        return filter.Operator switch { "contains" or "eq" => comparison == 0, "gte" => comparison >= 0, "lte" => comparison <= 0, _ => false };
    }
}
