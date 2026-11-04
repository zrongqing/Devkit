using Devkit.Application.Application.Authorization;
using Devkit.DomainCommons.Querying;

namespace Devkit.Application.Application.Navigation;

public sealed record MenuCompareQuery(MenuDeclaration[] Menus, PageQuery Query);
public sealed record MenuFieldsQuery(MenuDeclaration[] Menus, string MenuCode, PageQuery Query);
public sealed record MenuFieldRow(string Key, string Name, string LocalValue, string LastSyncedValue, string CurrentValue);

public sealed class MenuPageService(WebMenuService menus)
{
    public async Task<PageResult<MenuView>> TreeAsync(AuthorizationSubject actor, PageQuery query, CancellationToken ct)
    {
        var all = (await menus.ListAsync(actor, ct)).Menus;
        var fields = new Dictionary<string, Func<MenuView, object?>> { ["type"] = x => x.Type == "directory" ? "directory 目录" : "module 模块", ["enabled"] = x => x.Enabled ? "true 已启用" : "false 已停用" };
        var matched = PageQueries.SelectSnapshot(all, query, fields);
        var included = matched.Select(x => x.MenuCode).ToHashSet();
        var map = all.ToDictionary(x => x.MenuCode);
        foreach (var row in matched)
        {
            var parent = row.ParentCode;
            while (parent is not null && map.TryGetValue(parent, out var ancestor)) { included.Add(parent); parent = ancestor.ParentCode; }
        }
        var filtered = all.Where(x => included.Contains(x.MenuCode)).ToArray();
        var order = query with { Filters = [], Sorts = query.Sorts?.Length > 0 ? query.Sorts : [new("order"), new("menuCode")] };
        var roots = PageQueries.Snapshot(filtered.Where(x => x.ParentCode is null || !included.Contains(x.ParentCode)), order, fields);
        var rootCodes = roots.Items.Select(x => x.MenuCode).ToHashSet();
        bool InPage(MenuView row) => rootCodes.Contains(row.MenuCode) || row.ParentCode is not null && map.TryGetValue(row.ParentCode, out var parent) && InPage(parent);
        return roots with { Items = PageQueries.SelectSnapshot(filtered.Where(InPage), order, fields) };
    }
    public async Task<PageResult<MenuDifference>> CompareAsync(AuthorizationSubject actor, MenuCompareQuery request, CancellationToken ct)
    {
        var comparison = await menus.CompareAsync(actor, new(request.Menus), ct);
        return PageQueries.Snapshot(comparison.Items, request.Query, new Dictionary<string, Func<MenuDifference, object?>> { ["status"] = x => x.Status + " " + (x.Status switch { "new" => "未同步", "synced" => "已同步", "changed" => "前端声明变更", "missing" => "当前前端缺失", _ => "后台自建目录" }) });
    }
    public async Task<PageResult<MenuFieldRow>> FieldsAsync(AuthorizationSubject actor, MenuFieldsQuery request, CancellationToken ct)
    {
        var comparison = await menus.CompareAsync(actor, new(request.Menus), ct);
        var row = comparison.Items.FirstOrDefault(x => x.MenuCode == request.MenuCode) ?? throw new IdentityException(404, "not_found", "菜单不存在。");
        var fields = new[] { ("title", "名称"), ("parentCode", "父目录"), ("iconKey", "图标"), ("order", "排序"), ("type", "菜单类型"), ("routeKey", "页面路由"), ("requiredPermissions", "所需业务权限"), ("isClosable", "页签可关闭"), ("enabled", "后台启用状态") };
        static string Value(object? source, string key)
        {
            var value = source?.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase))?.GetValue(source);
            return value is null ? "—" : value is string[] values ? values.Length == 0 ? "无" : string.Join("、", values) : value.ToString() ?? "—";
        }
        return PageQueries.Snapshot(fields.Select(f => new MenuFieldRow(f.Item1, f.Item2, Value(row.Local, f.Item1), Value(row.Current?.LastDeclaration, f.Item1), Value(row.Current, f.Item1))), request.Query);
    }
}
