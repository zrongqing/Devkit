namespace Barcode2.Configuration;

public static class Barcode2Defaults
{
    public const string ProductionEnvironment = "production";
    public const string TestEnvironment = "test";
    public const string DevelopmentEnvironment = "development";

    private static readonly IReadOnlyDictionary<string, string> EnvironmentAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProductionEnvironment] = ProductionEnvironment,
            [TestEnvironment] = TestEnvironment,
            [DevelopmentEnvironment] = DevelopmentEnvironment
        };

    public static Barcode2Settings Create()
    {
        return new Barcode2Settings
        {
            Environments =
            [
                CreateEnvironment(ProductionEnvironment, "正式环境"),
                CreateEnvironment(TestEnvironment, "测试环境"),
                CreateEnvironment(DevelopmentEnvironment, "开发环境")
            ],
            ShareTargets =
            [
                CreateShare("development-primary", DevelopmentEnvironment, 10, "开发环境"),
                CreateShare("test-primary", TestEnvironment, 20, "测试环境"),
                CreateShare("production-primary", ProductionEnvironment, 30, "正式环境 1"),
                CreateShare("production-secondary", ProductionEnvironment, 31, "正式环境 2"),
                CreateShare("production-tertiary", ProductionEnvironment, 32, "正式环境 3")
            ]
        };
    }

    public static string ResolveEnvironmentKey(string environmentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentKey);
        if (EnvironmentAliases.TryGetValue(environmentKey.Trim(), out var resolved))
        {
            return resolved;
        }

        throw new ArgumentOutOfRangeException(
            nameof(environmentKey),
            environmentKey,
            "未知的 Barcode2 环境。");
    }

    private static Barcode2EnvironmentSettings CreateEnvironment(string key, string displayName)
    {
        return new Barcode2EnvironmentSettings
        {
            Key = key,
            DisplayName = displayName
        };
    }

    private static Barcode2ShareSettings CreateShare(
        string id,
        string targetKey,
        int order,
        string displayName)
    {
        return new Barcode2ShareSettings
        {
            Id = id,
            TargetKey = targetKey,
            Order = order,
            DisplayName = displayName
        };
    }
}
