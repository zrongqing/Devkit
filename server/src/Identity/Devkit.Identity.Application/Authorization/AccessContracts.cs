using System.Text.Json;

namespace Devkit.Identity.Application.Authorization;

public static class JsonData
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
}

public sealed class IdentityException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed record AuthorizationSubject(Guid Id, bool Administrator, IReadOnlyList<string> Permissions, IReadOnlyList<string>? MenuCodes = null)
{
    public void Require(string permission)
    {
        if (!Has(permission)) throw new IdentityException(403, "forbidden", "没有此功能的权限。");
    }
    public bool Has(string permission) => Administrator || PermissionCatalog.Expand(Permissions).Contains(permission)
        || permission == "exam-study.access" && Permissions.Any(x => x.StartsWith("study.", StringComparison.Ordinal));
    public void RequireAny(params string[] permissions)
    {
        if (!permissions.Any(Has)) throw new IdentityException(403, "forbidden", "没有此功能的权限。");
    }
    public void Own(Guid owner)
    {
        if (!Administrator && Id != owner) throw new IdentityException(404, "not_found", "内容不存在或无权访问。");
    }
}

public static class PermissionCatalog
{
    public static readonly ModulePermission[] Modules = [
        new("system.menus.manage", "菜单配置与同步", "系统管理", "system-menus"),
        new("system.monitor.view", "运行状态", "系统监控", "system-status"),
        new("study.projects.manage", "项目管理", "知识库", "study-project-management"),
        new("study.knowledge.manage", "知识库管理", "知识库", "study-knowledge"),
        new("study.search", "知识检索", "知识库", "study-projects"),
        new("study.practice", "刷题与模拟考、错题与学习记录", "知识库 / 刷题", "study-practice"),
        new("study.questions.manage", "题库管理", "知识库 / 刷题", "study-questions"),
        new("study.jobs.view", "处理任务", "系统监控", "study-jobs"),
        new("system.files.manage", "文件管理", "系统管理", "system-files"),
        new("system.storage.manage", "存储与迁移", "系统管理", "system-storage"),
        new("system.users.manage", "用户管理", "系统管理", "system-users"),
        new("system.roles.manage", "角色管理", "系统管理", "system-roles"),
        new("system.permissions.manage", "权限管理", "系统管理", "system-identity")
    ];
    public static readonly string[] All = ["exam-study.access", "system.identity.manage", .. Modules.Select(x => x.Key)];
    public static string[] Expand(IEnumerable<string> permissions)
    {
        var result = permissions.ToHashSet(StringComparer.Ordinal);
        if (result.Contains("exam-study.access")) result.UnionWith(Modules.Where(x => x.Key.StartsWith("study.")).Select(x => x.Key));
        if (result.Contains("system.identity.manage")) result.UnionWith(["system.users.manage", "system.roles.manage", "system.permissions.manage"]);
        return result.ToArray();
    }
}
public sealed record ModulePermission(string Key, string Name, string Group, string RouteKey);

public sealed record SuccessView(bool Ok = true);
public interface ICurrentIdentity { Task<AuthorizationSubject> CurrentAsync(CancellationToken ct = default); }
