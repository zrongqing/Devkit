using System.IO;
using Barcode2.Configuration;
using Devkit.Services.Configuration;
using Devkit.Services.Interfaces.Configuration;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class Barcode2ConfigurationServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"devkit-barcode2-settings-{Guid.NewGuid():N}");

    [Fact]
    public async Task First_read_creates_blank_semantic_configuration()
    {
        using var store = CreateStore();
        using var service = new Barcode2ConfigurationService(store);

        var settings = await service.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, settings.Environments.Count);
        Assert.Equal(5, settings.ShareTargets.Count);
        Assert.All(settings.Environments, environment =>
        {
            Assert.Empty(environment.DatabaseDataSource);
            Assert.Empty(environment.DatabaseUsername);
            Assert.Empty(environment.DatabasePassword);
            Assert.Empty(environment.PageBaseAddress);
        });
        Assert.All(settings.ShareTargets, target =>
        {
            Assert.Empty(target.Root);
            Assert.Empty(target.Username);
            Assert.Empty(target.Password);
        });
        Assert.True(await store.IsMigrationAppliedAsync(
            Barcode2ConfigurationService.Scope,
            Barcode2ConfigurationService.MigrationVersion,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Partial_configuration_can_be_saved_and_reloaded()
    {
        using var store = CreateStore();
        using (var service = new Barcode2ConfigurationService(store))
        {
            var settings = await service.GetAsync(TestContext.Current.CancellationToken);
            var development = settings.GetEnvironment(Barcode2Defaults.DevelopmentEnvironment);
            development.DatabaseDataSource = "db.example.invalid/service";
            settings.SourceCodePath = "source";
            await service.SaveAsync(settings, TestContext.Current.CancellationToken);
        }

        using var reloadedService = new Barcode2ConfigurationService(store);
        var reloaded = await reloadedService.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("source", reloaded.SourceCodePath);
        Assert.Equal(
            "db.example.invalid/service",
            reloaded.GetEnvironment(Barcode2Defaults.DevelopmentEnvironment).DatabaseDataSource);
        Assert.Empty(
            reloaded.GetEnvironment(Barcode2Defaults.DevelopmentEnvironment).DatabaseUsername);
    }

    [Fact]
    public async Task Database_operation_reports_missing_configuration()
    {
        using var store = CreateStore();
        using var service = new Barcode2ConfigurationService(store);

        var exception = await Assert.ThrowsAsync<Barcode2ConfigurationException>(() =>
            service.GetDatabaseConnectionStringAsync(
                Barcode2Defaults.DevelopmentEnvironment,
                TestContext.Current.CancellationToken));

        Assert.Contains("数据库配置不完整", exception.Message);
    }

    [Fact]
    public async Task Save_rejects_non_http_page_address()
    {
        using var store = CreateStore();
        using var service = new Barcode2ConfigurationService(store);
        var settings = await service.GetAsync(TestContext.Current.CancellationToken);
        settings.GetEnvironment(Barcode2Defaults.TestEnvironment).PageBaseAddress = "not-an-address";

        var exception = await Assert.ThrowsAsync<Barcode2ConfigurationException>(() =>
            service.SaveAsync(settings, TestContext.Current.CancellationToken));

        Assert.Contains("HTTP(S)", exception.Message);
    }

    [Fact]
    public async Task Save_rejects_non_unc_share_path()
    {
        using var store = CreateStore();
        using var service = new Barcode2ConfigurationService(store);
        var settings = await service.GetAsync(TestContext.Current.CancellationToken);
        settings.ShareTargets[0].Root = "relative/path";

        var exception = await Assert.ThrowsAsync<Barcode2ConfigurationException>(() =>
            service.SaveAsync(settings, TestContext.Current.CancellationToken));

        Assert.Contains("UNC", exception.Message);
    }

    [Fact]
    public async Task Configured_targets_are_returned_and_blank_slots_are_ignored()
    {
        using var store = CreateStore();
        using var service = new Barcode2ConfigurationService(store);
        var settings = await service.GetAsync(TestContext.Current.CancellationToken);
        var production = settings.ShareTargets
            .Where(target => target.TargetKey == Barcode2Defaults.ProductionEnvironment)
            .ToArray();
        production[0].Root = @"\\fileserver-a.example.invalid\webapp";
        production[0].Username = "first-user";
        production[0].Password = "first-password";
        production[2].Root = @"\\fileserver-c.example.invalid\webapp";
        await service.SaveAsync(settings, TestContext.Current.CancellationToken);

        var configured = (await service.GetAsync(TestContext.Current.CancellationToken))
            .GetShareTargets(Barcode2Defaults.ProductionEnvironment);

        Assert.Equal(2, configured.Count);
        Assert.Equal(["production-primary", "production-tertiary"],
            configured.Select(target => target.Id));
        Assert.Equal("first-user", configured[0].Username);
    }

    [Fact]
    public async Task Blank_target_group_reports_missing_configuration()
    {
        using var store = CreateStore();
        using var service = new Barcode2ConfigurationService(store);
        var settings = await service.GetAsync(TestContext.Current.CancellationToken);

        var exception = Assert.Throws<Barcode2ConfigurationException>(() =>
            settings.GetShareTargets(Barcode2Defaults.TestEnvironment));

        Assert.Contains("未配置 Webapp 发布目标", exception.Message);
    }

    [Fact]
    public async Task Existing_settings_without_selected_api_name_default_to_empty_and_can_be_saved()
    {
        using var store = CreateStore();
        using var service = new Barcode2ConfigurationService(store);
        await service.GetAsync(TestContext.Current.CancellationToken);
        var existingValues = await store.ReadScopeAsync(
            Barcode2ConfigurationService.Scope,
            TestContext.Current.CancellationToken);
        await store.WriteScopeAsync(
            Barcode2ConfigurationService.Scope,
            existingValues
                .Where(value => value.Key != "Preference.SelectedApiName")
                .Select(value => new LocalSettingValue(value.Key, value.Value))
                .ToList(),
            TestContext.Current.CancellationToken);

        var settings = await service.GetAsync(TestContext.Current.CancellationToken);
        Assert.Empty(settings.SelectedApiName);

        settings.SelectedApiName = "Api Name";
        await service.SaveAsync(settings, TestContext.Current.CancellationToken);
        Assert.Equal(
            "Api Name",
            (await service.GetAsync(TestContext.Current.CancellationToken)).SelectedApiName);
    }

    private SqliteLocalSettingsStore CreateStore()
    {
        Directory.CreateDirectory(_directory);
        return new SqliteLocalSettingsStore(Path.Combine(_directory, "settings.db"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
