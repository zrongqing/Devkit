using System.Text;
using System.IO;
using Devkit.Services.Configuration;
using Devkit.Services.Interfaces.Configuration;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class LocalSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"devkit-local-settings-{Guid.NewGuid():N}");

    [Fact]
    public async Task Values_round_trip_with_scope_isolation_and_secret_encryption()
    {
        var databasePath = Path.Combine(_directory, "settings.db");
        using var store = new SqliteLocalSettingsStore(databasePath);

        await store.WriteScopeAsync(
            "module-a",
            [
                new LocalSettingValue("Address", "http://localhost"),
                new LocalSettingValue("Username", "private-user", true),
                new LocalSettingValue("Password", "private-password", true)
            ],
            TestContext.Current.CancellationToken);
        await store.WriteScopeAsync(
            "module-b",
            [new LocalSettingValue("Address", "other")],
            TestContext.Current.CancellationToken);

        var first = await store.ReadScopeAsync(
            "module-a",
            TestContext.Current.CancellationToken);
        var second = await store.ReadScopeAsync(
            "module-b",
            TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost", first["Address"]);
        Assert.Equal("private-user", first["Username"]);
        Assert.Equal("private-password", first["Password"]);
        Assert.Equal("other", second["Address"]);
        var databaseText = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(
            databasePath,
            TestContext.Current.CancellationToken));
        Assert.DoesNotContain("private-user", databaseText);
        Assert.DoesNotContain("private-password", databaseText);
    }

    [Fact]
    public async Task Migration_is_atomic_and_applied_only_once()
    {
        using var store = new SqliteLocalSettingsStore(Path.Combine(_directory, "settings.db"));

        var first = await store.ApplyMigrationAsync(
            "module",
            1,
            [new LocalSettingValue("Value", "first")],
            TestContext.Current.CancellationToken);
        var second = await store.ApplyMigrationAsync(
            "module",
            1,
            [new LocalSettingValue("Value", "second")],
            TestContext.Current.CancellationToken);

        Assert.True(first);
        Assert.False(second);
        Assert.True(await store.IsMigrationAppliedAsync(
            "module",
            1,
            TestContext.Current.CancellationToken));
        Assert.Equal(
            "first",
            (await store.ReadScopeAsync("module", TestContext.Current.CancellationToken))["Value"]);
    }

    [Fact]
    public async Task Concurrent_writes_are_serialized_without_losing_unrelated_keys()
    {
        using var store = new SqliteLocalSettingsStore(Path.Combine(_directory, "settings.db"));
        var writes = Enumerable.Range(0, 12).Select(index =>
            store.WriteScopeAsync(
                "module",
                [new LocalSettingValue($"Key{index}", index.ToString())],
                TestContext.Current.CancellationToken));

        await Task.WhenAll(writes);

        var values = await store.ReadScopeAsync("module", TestContext.Current.CancellationToken);
        Assert.Equal(12, values.Count);
    }

    [Fact]
    public async Task Invalid_database_directory_reports_the_storage_failure()
    {
        Directory.CreateDirectory(_directory);
        var blockingFile = Path.Combine(_directory, "not-a-directory");
        await File.WriteAllTextAsync(
            blockingFile,
            "block",
            TestContext.Current.CancellationToken);
        using var store = new SqliteLocalSettingsStore(Path.Combine(blockingFile, "settings.db"));

        await Assert.ThrowsAnyAsync<Exception>(() => store.WriteScopeAsync(
            "module",
            [new LocalSettingValue("Key", "Value")],
            TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
