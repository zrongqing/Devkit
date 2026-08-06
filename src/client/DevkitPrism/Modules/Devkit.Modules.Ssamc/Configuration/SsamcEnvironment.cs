namespace Module.Ssamc.Configuration;

public static class SsamcEnvironment
{
    private const string Prefix = "DEVKIT_SSAMC_";

    public static string SourceCodePath =>
        Environment.GetEnvironmentVariable(Prefix + "SOURCE_PATH") ?? string.Empty;

    public static string WebappSourcePath =>
        Environment.GetEnvironmentVariable(Prefix + "WEBAPP_SOURCE_PATH") ?? string.Empty;

    public static string GetDatabaseConnection(string target)
    {
        return GetRequired($"DB_{Normalize(target)}_CONNECTION");
    }

    public static IReadOnlyList<SsamcShareTarget> GetShareTargets(string target)
    {
        var normalizedTarget = Normalize(target);
        var roots = GetRequired($"WEBAPP_{normalizedTarget}_ROOTS")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var username = GetRequired($"WEBAPP_{normalizedTarget}_USERNAME");
        var password = GetRequired($"WEBAPP_{normalizedTarget}_PASSWORD");

        return roots.Select(root => new SsamcShareTarget(root, username, password)).ToArray();
    }

    public static IReadOnlyList<string> GetOptionalShareRoots(string target)
    {
        var value = Environment.GetEnvironmentVariable(
            Prefix + $"WEBAPP_{Normalize(target)}_ROOTS");

        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string GetRequired(string suffix)
    {
        var variableName = Prefix + suffix;
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"缺少环境变量 {variableName}。");
        }

        return value;
    }

    private static string Normalize(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("目标环境不能为空。", nameof(target));
        }

        return target.Replace('.', '_').Replace('-', '_').ToUpperInvariant();
    }
}

public sealed record SsamcShareTarget(string Root, string Username, string Password);
