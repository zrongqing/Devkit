namespace Ssamc.Configuration;

public static class SsamcEnvironment
{
    private const string Prefix = "DEVKIT_SSAMC_";
    private static readonly IReadOnlyDictionary<string, string> DefaultDatabaseConnections =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProductionEnvironment] =
                "data source=192.168.10.68/ssamcerp;user id=barcode2_read;password=barcode2",
            [TestEnvironment] =
                "data source=192.168.20.54/ssamcerp;user id=barcode2;password=barcode2",
            [DevelopmentEnvironment] =
                "data source=192.168.215.58/ssamcerp;user id=barcode2;password=barcode2"
        };

    private static readonly IReadOnlyDictionary<string, string> DatabaseEnvironmentAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProductionEnvironment] = ProductionEnvironment,
            ["10.68"] = ProductionEnvironment,
            ["192.168.10.68"] = ProductionEnvironment,
            [TestEnvironment] = TestEnvironment,
            ["20.54"] = TestEnvironment,
            ["192.168.20.54"] = TestEnvironment,
            [DevelopmentEnvironment] = DevelopmentEnvironment,
            ["215.58"] = DevelopmentEnvironment,
            ["192.168.215.58"] = DevelopmentEnvironment
        };

    private static readonly IReadOnlyDictionary<string, string> LegacyDatabaseVariableSuffixes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProductionEnvironment] = "DB_10_68_CONNECTION",
            [TestEnvironment] = "DB_20_54_CONNECTION",
            [DevelopmentEnvironment] = "DB_215_58_CONNECTION"
        };

    public const string ProductionEnvironment = "production";
    public const string TestEnvironment = "test";
    public const string DevelopmentEnvironment = "development";

    public static string SourceCodePath =>
        Environment.GetEnvironmentVariable(Prefix + "SOURCE_PATH") ?? string.Empty;

    public static string WebappSourcePath =>
        Environment.GetEnvironmentVariable(Prefix + "WEBAPP_SOURCE_PATH") ?? string.Empty;

    public static string GetDatabaseConnection(string environmentKey)
    {
        var resolvedEnvironment = ResolveDatabaseEnvironment(environmentKey);
        var configuredConnection = GetOptional($"DB_{Normalize(resolvedEnvironment)}_CONNECTION");
        if (!string.IsNullOrWhiteSpace(configuredConnection))
        {
            return configuredConnection;
        }

        var legacyConnection = GetOptional(LegacyDatabaseVariableSuffixes[resolvedEnvironment]);
        return !string.IsNullOrWhiteSpace(legacyConnection)
                   ? legacyConnection
                   : DefaultDatabaseConnections[resolvedEnvironment];
    }

    public static string GetMenuDatabaseConnection(
        string environmentKey,
        string? configuredConnection = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredConnection))
        {
            return configuredConnection.Trim();
        }

        var resolvedEnvironment = ResolveDatabaseEnvironment(environmentKey);
        var environmentConnection = GetOptional($"MENU_DB_{Normalize(resolvedEnvironment)}_CONNECTION");
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

        return GetDatabaseConnection(resolvedEnvironment);
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

    private static string ResolveDatabaseEnvironment(string environmentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentKey);

        var value = environmentKey.Trim();
        if (DatabaseEnvironmentAliases.TryGetValue(value, out var resolvedEnvironment))
        {
            return resolvedEnvironment;
        }

        throw new ArgumentOutOfRangeException(
            nameof(environmentKey),
            environmentKey,
            "未知的 SSAMC 数据库环境。");
    }
}

public sealed record SsamcShareTarget(string Root, string Username, string Password);
public sealed record SsamcPageEnvironment(string Key, string DisplayName, string BaseAddress);
