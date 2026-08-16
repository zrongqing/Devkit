namespace Ssamc.Configuration;

public static class SsamcEnvironment
{
    private const string Prefix = "DEVKIT_SSAMC_";
    public const string ProductionEnvironment = "production";
    public const string TestEnvironment = "test";
    public const string DevelopmentEnvironment = "development";

    public static string SourceCodePath =>
        Environment.GetEnvironmentVariable(Prefix + "SOURCE_PATH") ?? string.Empty;

    public static string WebappSourcePath =>
        Environment.GetEnvironmentVariable(Prefix + "WEBAPP_SOURCE_PATH") ?? string.Empty;

    public static string GetDatabaseConnection(string target)
    {
        return GetRequired($"DB_{Normalize(target)}_CONNECTION");
    }

    public static string GetMenuDatabaseConnection(
        string environmentKey,
        string? configuredConnection = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredConnection))
        {
            return configuredConnection.Trim();
        }

        var normalizedEnvironment = Normalize(environmentKey);
        var environmentConnection = GetOptional($"MENU_DB_{normalizedEnvironment}_CONNECTION");
        if (!string.IsNullOrWhiteSpace(environmentConnection))
        {
            return environmentConnection;
        }

        // Keep the original single connection variable as a backwards-compatible override.
        var legacyConnection = GetOptional("MENU_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(legacyConnection))
        {
            return legacyConnection;
        }

        return environmentKey.ToLowerInvariant() switch
        {
            ProductionEnvironment =>
                "data source=192.168.10.68/ssamcerp;user id=barcode2_read;password=barcode2",
            TestEnvironment =>
                "data source=192.168.20.54/ssamcerp;user id=barcode2;password=barcode2",
            DevelopmentEnvironment =>
                "data source=192.168.215.57/ssamcerp;user id=barcode2;password=barcode2",
            _ => throw new ArgumentOutOfRangeException(
                     nameof(environmentKey),
                     environmentKey,
                     "未知的 SSAMC 菜单数据库环境。")
        };
    }

    public static IReadOnlyList<SsamcPageEnvironment> GetPageEnvironments()
    {
        return
        [
            new SsamcPageEnvironment(
                ProductionEnvironment,
                "正式环境",
                GetOptional("PAGE_PRODUCTION_BASE_URL", "http://192.168.10.41")),
            new SsamcPageEnvironment(
                TestEnvironment,
                "测试环境",
                GetOptional("PAGE_TEST_BASE_URL", "http://192.168.20.54")),
            new SsamcPageEnvironment(
                DevelopmentEnvironment,
                "开发环境",
                GetOptional("PAGE_DEVELOPMENT_BASE_URL", "http://192.168.215.57"))
        ];
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
            throw new SsamcConfigurationException($"缺少环境变量 {variableName}。");
        }

        return value;
    }

    private static string GetOptional(string suffix, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(Prefix + suffix);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? GetOptional(string suffix)
    {
        var value = Environment.GetEnvironmentVariable(Prefix + suffix);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
public sealed record SsamcPageEnvironment(string Key, string DisplayName, string BaseAddress);
