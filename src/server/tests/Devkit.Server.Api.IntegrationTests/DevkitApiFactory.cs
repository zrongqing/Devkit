using Devkit.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devkit.Server.Api.IntegrationTests;

internal sealed class DevkitApiFactory(
    bool registrationEnabled = true,
    bool failingCache = false,
    bool bootstrapEnabled = false)
    : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"devkit-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "Devkit.Tests",
                ["Jwt:Audience"] = "Devkit.Tests.Client",
                ["Jwt:SigningKey"] = "tests-only-signing-key-with-at-least-32-bytes",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Registration:Enabled"] = registrationEnabled.ToString(),
                ["BootstrapAccount:Enabled"] = bootstrapEnabled.ToString(),
                ["BootstrapAccount:UserName"] = "bootstrap-admin",
                ["BootstrapAccount:Email"] = "bootstrap@example.test",
                ["BootstrapAccount:Password"] = "BootstrapPassword123",
                ["Redis:ConnectionString"] = "localhost:6379,abortConnect=false,connectTimeout=100",
                ["Redis:ProfileTtlMinutes"] = "5",
                ["BackgroundJobs:RefreshTokenCleanup:Enabled"] = "false",
                ["BackgroundJobs:RefreshTokenCleanup:IntervalMinutes"] = "60"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DevkitDbContext>>();
            services.RemoveAll<DevkitDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<DevkitDbContext>>();

            var testOptions = new DbContextOptionsBuilder<DevkitDbContext>()
                .UseSqlite($"Data Source={_databasePath}")
                .Options;
            using (var setupContext = new DevkitDbContext(testOptions))
            {
                setupContext.Database.EnsureCreated();
            }

            services.AddDbContext<DevkitDbContext>((serviceProvider, options) =>
            {
                options.UseSqlite($"Data Source={_databasePath}");
                options.AddInterceptors(serviceProvider.GetRequiredService<AuditingSaveChangesInterceptor>());
            });

            services.RemoveAll<IDistributedCache>();
            if (failingCache)
            {
                services.AddSingleton<IDistributedCache, ThrowingDistributedCache>();
            }
            else
            {
                services.AddDistributedMemoryCache();
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_databasePath))
        {
            SqliteConnection.ClearAllPools();
            File.Delete(_databasePath);
        }
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw Unavailable();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromException<byte[]?>(Unavailable());
        public void Refresh(string key) => throw Unavailable();
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.FromException(Unavailable());
        public void Remove(string key) => throw Unavailable();
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.FromException(Unavailable());
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Unavailable();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.FromException(Unavailable());

        private static InvalidOperationException Unavailable() => new("Redis is unavailable in this test.");
    }
}
