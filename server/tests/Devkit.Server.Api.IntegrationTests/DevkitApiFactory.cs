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
using Microsoft.Extensions.Hosting;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Infrastructure.Workspace;

namespace Devkit.Server.Api.IntegrationTests;

internal sealed class DevkitApiFactory(
    bool registrationEnabled = true,
    bool failingCache = false,
    bool bootstrapEnabled = false,
    bool builtInAdmin = false)
    : WebApplicationFactory<Program>
{
    public string DataRoot { get; } = Path.Combine(
        Environment.GetEnvironmentVariable("DEVKIT_TEST_ROOT") ?? (OperatingSystem.IsWindows() ? @"D:\server data\devkit\temp\tests" : Path.GetTempPath()),
        $"devkit-tests-{Guid.NewGuid():N}");
    public TestExternalServices External { get; } = new();
    public TestModelGateway Model { get; } = new();
    private string DatabasePath => Path.Combine(DataRoot, "test.db");

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
                ["BootstrapAccount:Enabled"] = (bootstrapEnabled || builtInAdmin).ToString(),
                ["BootstrapAccount:UserName"] = builtInAdmin ? "admin" : "bootstrap-admin",
                ["BootstrapAccount:Email"] = builtInAdmin ? "admin@example.invalid" : "bootstrap@example.test",
                ["BootstrapAccount:Password"] = builtInAdmin ? "admin" : "BootstrapPassword123",
                ["Workspace:DataRoot"] = Path.Combine(DataRoot, "storage"),
                ["Workspace:MinimumFreeBytes"] = "0",
                ["Workspace:QdrantUrl"] = "http://qdrant.test",
                ["Redis:ConnectionString"] = "localhost:6379,abortConnect=false,connectTimeout=100",
                ["Redis:ProfileTtlMinutes"] = "5",
                ["BackgroundJobs:RefreshTokenCleanup:Enabled"] = "false",
                ["BackgroundJobs:RefreshTokenCleanup:IntervalMinutes"] = "60"
            }));
        builder.ConfigureServices(services =>
        {
            Directory.CreateDirectory(DataRoot);
            foreach (var hosted in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(WorkspaceWorker)).ToArray())
                services.Remove(hosted);
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(External);
            services.RemoveAll<IModelGateway>();
            services.AddSingleton<IModelGateway>(Model);
            services.RemoveAll<DbContextOptions<DevkitDbContext>>();
            services.RemoveAll<DevkitDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<DevkitDbContext>>();

            var testOptions = new DbContextOptionsBuilder<DevkitDbContext>()
                .UseSqlite($"Data Source={DatabasePath};Pooling=False")
                .Options;
            using (var setupContext = new DevkitDbContext(testOptions))
            {
                setupContext.Database.EnsureCreated();
            }

            services.AddDbContext<DevkitDbContext>((serviceProvider, options) =>
            {
                options.UseSqlite($"Data Source={DatabasePath};Pooling=False");
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
        if (disposing && Directory.Exists(DataRoot))
        {
            Directory.Delete(DataRoot, recursive: true);
        }
    }

    public async Task RunJobAsync(Guid jobId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevkitDbContext>();
        var job = await db.Set<Devkit.Server.Domain.Workspace.WorkJob>().SingleAsync(x => x.Id == jobId);
        await scope.ServiceProvider.GetRequiredService<WorkspaceJobHandler>().RunAsync(job, CancellationToken.None);
        job.Status = "completed";
        job.Progress = 100;
        await db.SaveChangesAsync();
    }

    public async Task InitializeStorageAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<LocalFileService>().EnsureDefaultAsync(CancellationToken.None);
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
