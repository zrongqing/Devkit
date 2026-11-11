using Devkit.DomainCommons.Authorization;
using System.Text.RegularExpressions;

namespace Devkit.Application.Application.Authorization;

public sealed class DeploymentPermissions(IEnumerable<string> moduleKeys)
{
    public ModulePermission[] Modules { get; } = [.. PermissionCatalog.Modules, .. Entries(moduleKeys)];
    public string[] All => [.. PermissionCatalog.All.Concat(Modules.Select(x => x.Key)).Distinct()];
    private static IEnumerable<ModulePermission> Entries(IEnumerable<string> keys)
    {
        var labels = new Dictionary<string, string> { ["view"] = "查看", ["edit"] = "维护", ["publish"] = "发布", ["audit"] = "审计" };
        foreach (var key in keys.Distinct(StringComparer.Ordinal))
        {
            if (!Regex.IsMatch(key, @"\A[a-z][a-z0-9._-]{0,63}\z")) throw new ArgumentException("CodeSystem:Modules 包含无效模块键。");
            foreach (var action in CodePermissions.Actions)
                yield return new(CodePermissions.For(key, action), $"{key} · {labels[action]}编码规则", "编码规则", "system-code-rules");
        }
    }
}
