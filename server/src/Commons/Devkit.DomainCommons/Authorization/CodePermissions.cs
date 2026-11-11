namespace Devkit.DomainCommons.Authorization;

public static class CodePermissions
{
    public const string MenuRead = "code.rules.view";
    public static readonly string[] Actions = ["view", "edit", "publish", "audit"];
    public static string For(string module, string action) => $"code.scope.{module}.{action}";

    public static IEnumerable<string> Expand(IEnumerable<string> permissions)
    {
        foreach (var permission in permissions)
        {
            if (!permission.StartsWith("code.scope.", StringComparison.Ordinal)) continue;
            int end = permission.LastIndexOf('.');
            if (end <= "code.scope.".Length || !Actions.Contains(permission[(end + 1)..])) continue;
            yield return MenuRead;
            yield return permission[..(end + 1)] + "view";
        }
    }
}
