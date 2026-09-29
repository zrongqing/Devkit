using System.Text.RegularExpressions;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.Navigation;

namespace Devkit.Server.Application.Navigation;

public sealed class WebMenuService(IWebMenuStore store, IWebMenuDefaults defaults)
{
    public const string ClaimType = "menu:web";
    private static readonly string[] EditableFields = ["title", "parentCode", "iconKey", "order"];
    private static BusinessException Invalid(string message) => new(400, "invalid_menu", message);
    private static BusinessException Conflict() => new(409, "menu_conflict", "菜单已变更，请重新比较或刷新。");

    public async Task<WebMenuSnapshot> SnapshotAsync(CancellationToken ct)
    {
        var snapshot = await store.ReadAsync(ct);
        if (snapshot.Version != 0) return snapshot;
        var menus = defaults.Create();
        ValidateTree(menus); Protect(menus);
        var grants = PermissionCatalog.All.ToDictionary(p => p, p => menus
            .Where(m => m.Type == "module" && m.MenuCode != "system.menus" && !m.IsPublic
                && JsonData.Read<string[]>(m.RequiredPermissionsJson).All(PermissionCatalog.Expand([p]).Contains))
            .Select(m => m.MenuCode).ToArray());
        try { await store.CommitAsync(0, menus, grants, ct); }
        catch (BusinessException e) when (e.Code == "menu_conflict") { /* Another request initialized the same database. */ }
        return await store.ReadAsync(ct);
    }

    public async Task<MenuTreeView> ListAsync(Actor actor, CancellationToken ct)
    {
        actor.RequireAny("system.menus.manage", "system.users.manage", "system.roles.manage", "system.permissions.manage");
        return View(await SnapshotAsync(ct));
    }
    public async Task<IReadOnlyList<WebMenu>> NavigationAsync(Actor? actor, CancellationToken ct)
    {
        var all = (await SnapshotAsync(ct)).Menus.ToDictionary(x => x.MenuCode);
        bool Enabled(WebMenu menu) => menu.Enabled && (menu.ParentCode is null || all.TryGetValue(menu.ParentCode, out var parent) && Enabled(parent));
        var allowed = new HashSet<string>();
        foreach (var menu in all.Values.Where(x => x.Type == "module" && Enabled(x)
            && (x.IsPublic || actor is not null && (actor.Administrator || (actor.MenuCodes ?? []).Contains(x.MenuCode)))))
        {
            var current = menu;
            allowed.Add(current.MenuCode);
            while (current.ParentCode is not null) { current = all[current.ParentCode]; allowed.Add(current.MenuCode); }
        }
        return all.Values.Where(x => allowed.Contains(x.MenuCode)).OrderBy(x => x.Order).ThenBy(x => x.MenuCode).ToArray();
    }

    public async Task<MenuComparison> CompareAsync(Actor actor, MenuManifestRequest request, CancellationToken ct)
    {
        actor.Require("system.menus.manage");
        var local = Manifest(request.Menus);
        var snapshot = await SnapshotAsync(ct);
        var stored = snapshot.Menus.ToDictionary(x => x.MenuCode);
        var differences = local.Values.Select(d => {
            stored.TryGetValue(d.MenuCode, out var current);
            var status = current is null ? "new" : current.DeclarationJson is not null && JsonData.Write(JsonData.Read<MenuDeclaration>(current.DeclarationJson)) == JsonData.Write(d) ? "synced" : "changed";
            return new MenuDifference(d.MenuCode, status, d, current is null ? null : View(current));
        }).ToList();
        differences.AddRange(stored.Values.Where(x => !local.ContainsKey(x.MenuCode)).Select(x => new MenuDifference(x.MenuCode,
            x.Source == "manual" ? "manual" : "missing", null, View(x))));
        return new(snapshot.Version, differences);
    }

    public async Task<MenuTreeView> SyncAsync(Actor actor, MenuSyncRequest request, CancellationToken ct)
    {
        actor.Require("system.menus.manage");
        var local = Manifest(request.Menus);
        if (request.Items is null || request.Items.Length == 0 || request.Items.Length > 1000
            || request.Items.Any(x => x is null || x.MenuCode is null || !local.ContainsKey(x.MenuCode) || x.ApplyFields is null || x.ApplyFields.Except(EditableFields).Any())
            || request.Items.Select(x => x.MenuCode).Distinct().Count() != request.Items.Length) throw Invalid("请选择有效的菜单和同步字段。");
        var snapshot = await SnapshotAsync(ct);
        if (snapshot.Version != request.Version) throw Conflict();
        var menus = snapshot.Menus.ToDictionary(x => x.MenuCode);
        var processing = new HashSet<string>();
        void Apply(string code, string[] fields)
        {
            if (!processing.Add(code)) return;
            var d = local[code];
            // Existing ancestors retain their configuration; only missing ancestors are created.
            if (d.ParentCode is not null && !menus.ContainsKey(d.ParentCode)) Apply(d.ParentCode, []);
            if (!menus.TryGetValue(code, out var menu)) menus.Add(code, FromDeclaration(d));
            else
            {
                if (menu.Type != d.Type || menu.RouteKey != d.RouteKey) throw Invalid($"菜单 {code} 的类型和页面映射不可更改。");
                if (fields.Contains("title")) menu.Title = d.Title;
                if (fields.Contains("parentCode")) menu.ParentCode = d.ParentCode;
                if (fields.Contains("iconKey")) menu.IconKey = d.IconKey;
                if (fields.Contains("order")) menu.Order = d.Order;
                menu.DeclarationJson = JsonData.Write(d);
                menu.RequiredPermissionsJson = JsonData.Write(d.RequiredPermissions);
                menu.Revision++;
            }
        }
        // Process selected ancestors first so explicit field selections are not lost to dependency creation.
        foreach (var item in request.Items.OrderBy(x => Depth(local[x.MenuCode], local))) Apply(item.MenuCode, item.ApplyFields);
        ValidateTree(menus.Values); Protect(menus.Values);
        await store.CommitAsync(snapshot.Version, menus.Values.ToArray(), null, ct);
        return View(await store.ReadAsync(ct));
    }

    public async Task<MenuTreeView> CreateDirectoryAsync(Actor actor, DirectoryCreateRequest request, CancellationToken ct)
    {
        actor.Require("system.menus.manage");
        var snapshot = await SnapshotAsync(ct);
        if (snapshot.Version != request.Version) throw Conflict();
        if (snapshot.Menus.Any(x => x.MenuCode == request.MenuCode)) throw Invalid("菜单编码已存在。");
        var menu = FromDeclaration(new(request.MenuCode, "directory", request.ParentCode, request.Title, null, request.IconKey, request.Order, []));
        menu.Source = "manual"; menu.DeclarationJson = null;
        var menus = snapshot.Menus.Append(menu).ToArray(); ValidateTree(menus);
        await store.CommitAsync(snapshot.Version, menus, null, ct);
        return View(await store.ReadAsync(ct));
    }
    public async Task<MenuTreeView> EditAsync(Actor actor, string code, MenuEditRequest request, CancellationToken ct)
    {
        actor.Require("system.menus.manage");
        var snapshot = await SnapshotAsync(ct);
        if (snapshot.Version != request.Version) throw Conflict();
        var menu = snapshot.Menus.SingleOrDefault(x => x.MenuCode == code) ?? throw new BusinessException(404, "not_found", "菜单不存在。");
        menu.Title = request.Title; menu.ParentCode = request.ParentCode; menu.IconKey = request.IconKey;
        menu.Order = request.Order; menu.Enabled = request.Enabled; menu.Revision++;
        ValidateTree(snapshot.Menus); Protect(snapshot.Menus);
        await store.CommitAsync(snapshot.Version, snapshot.Menus, null, ct);
        return View(await store.ReadAsync(ct));
    }
    public async Task<MenuTreeView> DeleteDirectoryAsync(Actor actor, string code, int version, CancellationToken ct)
    {
        actor.Require("system.menus.manage");
        var snapshot = await SnapshotAsync(ct);
        if (snapshot.Version != version) throw Conflict();
        var menu = snapshot.Menus.SingleOrDefault(x => x.MenuCode == code) ?? throw new BusinessException(404, "not_found", "菜单不存在。");
        if (menu.Type != "directory" || snapshot.Menus.Any(x => x.ParentCode == code)) throw Invalid("只能删除空目录；模块菜单请使用停用。");
        var menus = snapshot.Menus.Where(x => x.MenuCode != code).ToArray(); Protect(menus);
        await store.CommitAsync(snapshot.Version, menus, null, ct);
        return View(await store.ReadAsync(ct));
    }
    public async Task ValidateGrantsAsync(Actor actor, string[]? codes, CancellationToken ct)
    {
        if (codes is null || codes.Length > 1000) throw Invalid("菜单授权列表无效。");
        var modules = (await SnapshotAsync(ct)).Menus.Where(x => x.Type == "module").Select(x => x.MenuCode).ToHashSet();
        if (codes.Any(x => x is null || !modules.Contains(x))) throw Invalid("只能授权已入库的模块菜单。");
        if (!actor.Administrator && codes.Except(actor.MenuCodes ?? []).Any()) throw new BusinessException(403, "forbidden", "不能授予自己没有的菜单访问权。");
    }

    private static Dictionary<string, MenuDeclaration> Manifest(MenuDeclaration[]? declarations)
    {
        if (declarations is null || declarations.Length > 1000 || declarations.Any(x => x is null || x.RequiredPermissions is null)) throw Invalid("前端菜单声明无效。");
        foreach (var d in declarations)
            if (d.RequiredPermissions.Any(p => !PermissionCatalog.All.Contains(p))) throw Invalid("声明包含未知业务权限。");
        var normalized = declarations.Select(d => d with { RequiredPermissions = d.RequiredPermissions.Distinct().Order().ToArray() }).ToArray();
        ValidateTree(normalized.Select(FromDeclaration));
        return normalized.ToDictionary(x => x.MenuCode);
    }
    private static int Depth(MenuDeclaration d, Dictionary<string, MenuDeclaration> all) => d.ParentCode is null ? 0 : 1 + Depth(all[d.ParentCode], all);
    public static WebMenu FromDeclaration(MenuDeclaration d) => new()
    {
        MenuCode = d.MenuCode, Type = d.Type, ParentCode = d.ParentCode, Title = d.Title,
        RouteKey = d.RouteKey, IconKey = d.IconKey, Order = d.Order, IsClosable = d.IsClosable,
        RequiredPermissionsJson = JsonData.Write(d.RequiredPermissions.Distinct().Order().ToArray()),
        DeclarationJson = JsonData.Write(d with { RequiredPermissions = d.RequiredPermissions.Distinct().Order().ToArray() })
    };
    private static void ValidateTree(IEnumerable<WebMenu> entries)
    {
        var menus = entries.ToArray();
        if (menus.Any(x => !ValidCode(x.MenuCode) || string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 120 || x.IconKey?.Length > 80
            || x.Type is not ("directory" or "module") || x.Type == "directory" && x.RouteKey is not null
            || x.Type == "module" && !ValidRoute(x.RouteKey))) throw Invalid("菜单编码、类型、名称或页面映射无效。");
        if (menus.Select(x => x.MenuCode).Distinct().Count() != menus.Length
            || menus.Where(x => x.Type == "module").Select(x => x.RouteKey).Distinct().Count() != menus.Count(x => x.Type == "module")) throw Invalid("菜单编码和页面映射必须唯一。");
        var all = menus.ToDictionary(x => x.MenuCode);
        foreach (var menu in menus)
        {
            var visited = new HashSet<string> { menu.MenuCode }; var current = menu;
            while (current.ParentCode is not null)
            {
                if (!all.TryGetValue(current.ParentCode, out var parent) || parent.Type != "directory") throw Invalid("父级必须是存在的目录菜单。");
                if (!visited.Add(parent.MenuCode) || visited.Count > 32) throw Invalid("菜单存在循环或层级超过 32 层。");
                current = parent;
            }
        }
    }
    private static bool ValidCode(string? code) => code is not null && code.Length <= 120 && code == code.Trim() && Regex.IsMatch(code, "^[a-z][a-z0-9]*(\\.[a-z][a-z0-9]*)*$", RegexOptions.CultureInvariant);
    private static bool ValidRoute(string? route) => route is not null && Regex.IsMatch(route, "^[a-z][a-z0-9._-]{0,119}$", RegexOptions.CultureInvariant);
    private static void Protect(IEnumerable<WebMenu> entries)
    {
        var all = entries.ToDictionary(x => x.MenuCode);
        foreach (var (code, route) in new[] { ("home", "home"), ("system.menus", "system-menus") })
        {
            if (!all.TryGetValue(code, out var menu) || menu.Type != "module" || menu.RouteKey != route) throw Invalid("不能移除首页或菜单管理入口。");
            while (true)
            {
                if (!menu.Enabled) throw Invalid("不能停用首页、菜单管理或其上级目录。");
                if (menu.ParentCode is null) break;
                menu = all[menu.ParentCode];
            }
        }
        if (all["home"].ParentCode is not null || all["home"].IsClosable) throw Invalid("首页须保留为不可关闭的顶层页面。");
    }
    private static MenuTreeView View(WebMenuSnapshot snapshot) => new(snapshot.Version, snapshot.Menus.OrderBy(x => x.Order).ThenBy(x => x.MenuCode).Select(View).ToArray());
    private static MenuView View(WebMenu x) => new(x.MenuCode, x.Type, x.ParentCode, x.Title, x.RouteKey, x.IconKey,
        x.Order, x.Enabled, x.IsPublic, x.IsClosable, x.Source, JsonData.Read<string[]>(x.RequiredPermissionsJson),
        x.Revision, x.DeclarationJson is null ? null : JsonData.Read<MenuDeclaration>(x.DeclarationJson));
}
