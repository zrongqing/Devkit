using Devkit.Business.Application.Abstractions;
using Devkit.Business.Application.Workspace;
using Devkit.DomainCommons.Authorization;

namespace Devkit.Business.Application.CodeSystem;

public sealed class CodeAccess(IIdentitySession identity, CodeSettings settings)
{
    public Actor User => identity.Current ?? throw new BusinessException(401, "unauthorized", "请先登录。");
    public string[] Modules(string action)
    {
        var actor = User;
        var all = settings.Scenarios.Select(x => x.ModuleKey).Distinct();
        if (actor.Administrator) return all.ToArray();
        var grants = actor.Permissions.Concat(CodePermissions.Expand(actor.Permissions)).ToHashSet(StringComparer.Ordinal);
        if (!grants.Any(p => p.StartsWith("code.scope.", StringComparison.Ordinal) && p.EndsWith("." + action, StringComparison.Ordinal)))
            throw new BusinessException(403, "forbidden", "没有此操作的权限。");
        return all.Where(module => grants.Contains(CodePermissions.For(module, action))).ToArray();
    }

    public CodeScenario Scenario(string key, string action)
    {
        var modules = Modules(action);
        return settings.Scenarios.SingleOrDefault(s => s.RuleKey == key && modules.Contains(s.ModuleKey))
            ?? throw new BusinessException(404, "scenario_not_found", "场景不存在或无权访问。");
    }
}
