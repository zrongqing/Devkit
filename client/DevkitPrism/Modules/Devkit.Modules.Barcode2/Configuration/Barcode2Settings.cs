namespace Barcode2.Configuration;

public sealed class Barcode2Settings
{
    public string SourceCodePath { get; set; } = string.Empty;
    public string WebappSourcePath { get; set; } = string.Empty;
    public string WebappUpdateSourcePath { get; set; } = string.Empty;
    public string WebappPublishPath { get; set; } = string.Empty;
    public string SelectedPageEnvironmentKey { get; set; } = Barcode2Defaults.DevelopmentEnvironment;
    public string SelectedApiCode { get; set; } = string.Empty;
    public string SelectedApiName { get; set; } = string.Empty;
    public List<Barcode2EnvironmentSettings> Environments { get; set; } = [];
    public List<Barcode2ShareSettings> ShareTargets { get; set; } = [];

    public Barcode2EnvironmentSettings GetEnvironment(string environmentKey)
    {
        var resolvedKey = Barcode2Defaults.ResolveEnvironmentKey(environmentKey);
        return Environments.FirstOrDefault(environment =>
                   environment.Key.Equals(resolvedKey, StringComparison.OrdinalIgnoreCase))
               ?? throw new Barcode2ConfigurationException($"缺少 {resolvedKey} 环境配置。");
    }

    public IReadOnlyList<Barcode2ShareSettings> GetShareTargets(string targetKey, string? rootOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKey);
        var resolvedKey = Barcode2Defaults.ResolveEnvironmentKey(targetKey);
        var publishPath = rootOverride?.Trim();
        var environmentTargets = ShareTargets.Where(target =>
                target.TargetKey.Equals(resolvedKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var configuredTargets = environmentTargets.Where(target => !string.IsNullOrWhiteSpace(target.Root))
            .ToArray();
        if (configuredTargets.Length == 0 && !string.IsNullOrWhiteSpace(publishPath))
        {
            configuredTargets = environmentTargets.Take(1).ToArray();
        }

        var targets = configuredTargets
            .Select(target => new Barcode2ShareSettings
            {
                Id = target.Id,
                TargetKey = target.TargetKey,
                Order = target.Order,
                DisplayName = target.DisplayName,
                Root = string.IsNullOrWhiteSpace(publishPath) ? target.Root : publishPath,
                Username = target.Username,
                Password = target.Password
            })
            .ToArray();
        if (targets.Length == 0)
        {
            throw new Barcode2ConfigurationException(
                $"{resolvedKey} 环境未配置 Webapp 发布目标。");
        }

        if (targets.Any(target =>
                !target.Root.Trim().StartsWith(@"\\", StringComparison.Ordinal)))
        {
            throw new Barcode2ConfigurationException(
                $"{resolvedKey} 环境包含无效的 UNC 共享路径。");
        }

        return targets;
    }
}

public sealed class Barcode2EnvironmentSettings
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DatabaseDataSource { get; set; } = string.Empty;
    public string DatabaseUsername { get; set; } = string.Empty;
    public string DatabasePassword { get; set; } = string.Empty;
    public string PageBaseAddress { get; set; } = string.Empty;
}

public sealed class Barcode2ShareSettings
{
    public string Id { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public int Order { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Root { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed record Barcode2PageEnvironment(string Key, string DisplayName, string BaseAddress);
public sealed record Barcode2ShareTarget(string Root, string Username, string Password);
