using Barcode2.Configuration;

namespace Devkit.Loading.Tests;

internal sealed class Barcode2TestConfigurationService : IBarcode2ConfigurationService
{
    public Barcode2Settings Settings { get; set; } = CreateSettings();

    public Dictionary<string, string> ConnectionStrings { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<Barcode2Settings> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Settings);

    public Task SaveAsync(
        Barcode2Settings settings,
        CancellationToken cancellationToken = default)
    {
        Barcode2ConfigurationService.Validate(settings);
        Settings = settings;
        return Task.CompletedTask;
    }

    public Task<string> GetDatabaseConnectionStringAsync(
        string environmentKey,
        CancellationToken cancellationToken = default)
    {
        var key = Barcode2Defaults.ResolveEnvironmentKey(environmentKey);
        return Task.FromResult(
            ConnectionStrings.TryGetValue(key, out var connection)
                ? connection
                : Barcode2ConfigurationService.BuildConnectionString(Settings.GetEnvironment(key)));
    }

    private static Barcode2Settings CreateSettings()
    {
        var settings = Barcode2Defaults.Create();
        foreach (var environment in settings.Environments)
        {
            environment.DatabaseDataSource = $"{environment.Key}-db.example.invalid/service";
            environment.DatabaseUsername = "test-user";
            environment.DatabasePassword = "test-password";
            environment.PageBaseAddress = $"https://{environment.Key}.example.invalid";
        }

        foreach (var target in settings.ShareTargets)
        {
            target.Root = $@"\\{target.Id}.example.invalid\webapp";
            target.Username = "test-user";
            target.Password = "test-password";
        }

        return settings;
    }
}
