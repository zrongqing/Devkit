using System.Data.Common;
using Devkit.Services.Interfaces.Configuration;

namespace Barcode2.Configuration;

public sealed class Barcode2ConfigurationService : IBarcode2ConfigurationService, IDisposable
{
    internal const string Scope = "barcode2";
    internal const int MigrationVersion = 1;

    private readonly ILocalSettingsStore _settingsStore;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public Barcode2ConfigurationService(ILocalSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public async Task<Barcode2Settings> GetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return Deserialize(await _settingsStore.ReadScopeAsync(Scope, cancellationToken));
    }

    public async Task SaveAsync(
        Barcode2Settings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);
        await EnsureInitializedAsync(cancellationToken);
        await _settingsStore.WriteScopeAsync(Scope, Serialize(settings), cancellationToken);
    }

    public async Task<string> GetDatabaseConnectionStringAsync(
        string environmentKey,
        CancellationToken cancellationToken = default)
    {
        var environment = (await GetAsync(cancellationToken)).GetEnvironment(environmentKey);
        return BuildConnectionString(environment);
    }

    public void Dispose() => _initializationLock.Dispose();

    internal static string BuildConnectionString(Barcode2EnvironmentSettings environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ValidateDatabase(environment);
        var builder = new DbConnectionStringBuilder
        {
            ["Data Source"] = environment.DatabaseDataSource.Trim(),
            ["User Id"] = environment.DatabaseUsername.Trim(),
            ["Password"] = environment.DatabasePassword
        };
        return builder.ConnectionString;
    }

    internal static void ValidatePageAddress(Barcode2EnvironmentSettings environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (string.IsNullOrWhiteSpace(environment.PageBaseAddress))
        {
            throw new Barcode2ConfigurationException(
                $"{environment.DisplayName}的页面/后端地址未配置。");
        }

        if (!Uri.TryCreate(environment.PageBaseAddress, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new Barcode2ConfigurationException(
                $"{environment.DisplayName}的页面/后端地址必须是 HTTP(S) 绝对地址。");
        }
    }

    internal static void Validate(Barcode2Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Environments.Select(environment => environment.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != settings.Environments.Count)
        {
            throw new Barcode2ConfigurationException("环境配置键不能重复。");
        }

        foreach (var key in EnvironmentKeys())
        {
            var environment = settings.GetEnvironment(key);
            if (string.IsNullOrWhiteSpace(environment.DisplayName))
            {
                throw new Barcode2ConfigurationException($"{key} 环境显示名称不能为空。");
            }

            if (!string.IsNullOrWhiteSpace(environment.PageBaseAddress))
            {
                ValidatePageAddress(environment);
            }
        }

        if (settings.ShareTargets.Select(target => target.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != settings.ShareTargets.Count)
        {
            throw new Barcode2ConfigurationException("Webapp 发布目录标识不能重复。");
        }

        foreach (var target in settings.ShareTargets)
        {
            if (string.IsNullOrWhiteSpace(target.Id) ||
                string.IsNullOrWhiteSpace(target.TargetKey) ||
                string.IsNullOrWhiteSpace(target.DisplayName))
            {
                throw new Barcode2ConfigurationException(
                    "Webapp 发布目标的标识、环境和显示名称不能为空。");
            }

            try
            {
                _ = Barcode2Defaults.ResolveEnvironmentKey(target.TargetKey);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new Barcode2ConfigurationException(
                    $"{target.DisplayName}使用了未知的发布环境。",
                    exception);
            }

            if (!string.IsNullOrWhiteSpace(target.Root) &&
                !target.Root.Trim().StartsWith(@"\\", StringComparison.Ordinal))
            {
                throw new Barcode2ConfigurationException(
                    $"{target.DisplayName}必须配置有效的 UNC 共享路径。");
            }
        }

        foreach (var key in EnvironmentKeys())
        {
            if (!settings.ShareTargets.Any(target =>
                    target.TargetKey.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                throw new Barcode2ConfigurationException($"缺少 {key} Webapp 发布目标槽位。");
            }
        }

        try
        {
            _ = Barcode2Defaults.ResolveEnvironmentKey(settings.SelectedPageEnvironmentKey);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new Barcode2ConfigurationException("页面环境选择无效。", exception);
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            if (!await _settingsStore.IsMigrationAppliedAsync(
                    Scope,
                    MigrationVersion,
                    cancellationToken))
            {
                var values = await _settingsStore.ReadScopeAsync(Scope, cancellationToken);
                var settings = values.Count == 0 ? Barcode2Defaults.Create() : Deserialize(values);
                Validate(settings);
                await _settingsStore.ApplyMigrationAsync(
                    Scope,
                    MigrationVersion,
                    Serialize(settings),
                    cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static void ValidateDatabase(Barcode2EnvironmentSettings environment)
    {
        if (string.IsNullOrWhiteSpace(environment.DatabaseDataSource) ||
            string.IsNullOrWhiteSpace(environment.DatabaseUsername) ||
            string.IsNullOrWhiteSpace(environment.DatabasePassword))
        {
            throw new Barcode2ConfigurationException(
                $"{environment.DisplayName}的数据库配置不完整。");
        }
    }

    private static IReadOnlyCollection<LocalSettingValue> Serialize(Barcode2Settings settings)
    {
        var values = new List<LocalSettingValue>
        {
            new("General.SourceCodePath", TrimOrEmpty(settings.SourceCodePath)),
            new("General.WebappSourcePath", TrimOrEmpty(settings.WebappSourcePath)),
            new("Preference.SelectedPageEnvironment", settings.SelectedPageEnvironmentKey),
            new("Preference.SelectedApiCode", settings.SelectedApiCode ?? string.Empty),
            new("Preference.SelectedApiName", settings.SelectedApiName ?? string.Empty)
        };

        foreach (var environment in settings.Environments)
        {
            var prefix = $"Environment.{environment.Key}.";
            values.Add(new LocalSettingValue(prefix + "DisplayName", environment.DisplayName));
            values.Add(new LocalSettingValue(
                prefix + "DatabaseDataSource",
                TrimOrEmpty(environment.DatabaseDataSource)));
            values.Add(new LocalSettingValue(
                prefix + "DatabaseUsername",
                TrimOrEmpty(environment.DatabaseUsername),
                true));
            values.Add(new LocalSettingValue(
                prefix + "DatabasePassword",
                environment.DatabasePassword ?? string.Empty,
                true));
            values.Add(new LocalSettingValue(
                prefix + "PageBaseAddress",
                TrimOrEmpty(environment.PageBaseAddress)));
        }

        foreach (var target in settings.ShareTargets)
        {
            var prefix = $"Share.{target.Id}.";
            values.Add(new LocalSettingValue(prefix + "TargetKey", target.TargetKey));
            values.Add(new LocalSettingValue(prefix + "Order", target.Order.ToString()));
            values.Add(new LocalSettingValue(prefix + "DisplayName", target.DisplayName));
            values.Add(new LocalSettingValue(prefix + "Root", TrimOrEmpty(target.Root)));
            values.Add(new LocalSettingValue(
                prefix + "Username",
                TrimOrEmpty(target.Username),
                true));
            values.Add(new LocalSettingValue(
                prefix + "Password",
                target.Password ?? string.Empty,
                true));
        }

        return values;
    }

    private static Barcode2Settings Deserialize(IReadOnlyDictionary<string, string> values)
    {
        string Required(string key) => values.TryGetValue(key, out var value)
                                           ? value
                                           : throw new Barcode2ConfigurationException(
                                               $"本地配置缺少 {key}。");

        var settings = new Barcode2Settings
        {
            SourceCodePath = Required("General.SourceCodePath"),
            WebappSourcePath = Required("General.WebappSourcePath"),
            SelectedPageEnvironmentKey = Required("Preference.SelectedPageEnvironment"),
            SelectedApiCode = Required("Preference.SelectedApiCode"),
            SelectedApiName = values.TryGetValue(
                "Preference.SelectedApiName",
                out var selectedApiName)
                                   ? selectedApiName
                                   : string.Empty
        };

        foreach (var key in EnvironmentKeys())
        {
            var prefix = $"Environment.{key}.";
            settings.Environments.Add(new Barcode2EnvironmentSettings
            {
                Key = key,
                DisplayName = Required(prefix + "DisplayName"),
                DatabaseDataSource = Required(prefix + "DatabaseDataSource"),
                DatabaseUsername = Required(prefix + "DatabaseUsername"),
                DatabasePassword = Required(prefix + "DatabasePassword"),
                PageBaseAddress = Required(prefix + "PageBaseAddress")
            });
        }

        var shareIds = values.Keys
            .Where(key => key.StartsWith("Share.", StringComparison.OrdinalIgnoreCase) &&
                          key.EndsWith(".TargetKey", StringComparison.OrdinalIgnoreCase))
            .Select(key => key["Share.".Length..^".TargetKey".Length])
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var id in shareIds)
        {
            var prefix = $"Share.{id}.";
            settings.ShareTargets.Add(new Barcode2ShareSettings
            {
                Id = id,
                TargetKey = Required(prefix + "TargetKey"),
                Order = int.TryParse(Required(prefix + "Order"), out var order) ? order : 100,
                DisplayName = Required(prefix + "DisplayName"),
                Root = Required(prefix + "Root"),
                Username = Required(prefix + "Username"),
                Password = Required(prefix + "Password")
            });
        }

        settings.ShareTargets = settings.ShareTargets.OrderBy(target => target.Order).ToList();
        Validate(settings);
        return settings;
    }

    private static IEnumerable<string> EnvironmentKeys()
    {
        yield return Barcode2Defaults.ProductionEnvironment;
        yield return Barcode2Defaults.TestEnvironment;
        yield return Barcode2Defaults.DevelopmentEnvironment;
    }

    private static string TrimOrEmpty(string? value) => value?.Trim() ?? string.Empty;
}
