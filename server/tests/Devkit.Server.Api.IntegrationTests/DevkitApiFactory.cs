using System.Net;
using System.Security.Cryptography;
using Devkit.Business.Application.Workspace;
using Devkit.Business.Infrastructure.Persistence;
using Devkit.Business.Infrastructure.Workspace;
using Devkit.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Devkit.Server.Api.IntegrationTests;

// Two actual application hosts, independent SQLite databases and an HTTP boundary.
// The routing client represents the public gateway, not a combined application.
internal sealed class DevkitApiFactory : IDisposable
{
    private readonly IdentityHost identity;
    private readonly BusinessHost business;
    private readonly string testRoot;
    public string DataRoot { get; }
    public string PrivatePath => Path.Combine(DataRoot, "identity.pem");
    public string PublicPath => Path.Combine(DataRoot, "business.pem");
    public string IdentityDatabasePath => Path.Combine(DataRoot, "identity.db");
    public string BusinessDatabasePath => Path.Combine(DataRoot, "business.db");
    public TestExternalServices External { get; } = new();
    public TestModelGateway Model { get; } = new();
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? IdentityOverride { get; set; }
    public int IdentityRequests { get; private set; }
    public IServiceProvider Services => business.Services;
    public IServiceProvider IdentityServices => identity.Services;

    public DevkitApiFactory(bool registrationEnabled = true, bool failingCache = false,
        bool bootstrapEnabled = false, bool builtInAdmin = false,
        Action<IdentityDbContext>? prepareDatabase = null,
        IReadOnlyDictionary<string, string?>? identityConfiguration = null,
        IReadOnlyDictionary<string, string?>? businessConfiguration = null)
    {
        testRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEVKIT_TEST_ROOT") ?? Path.GetTempPath());
        DataRoot = Path.Combine(testRoot, $"devkit-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DataRoot);
        using var rsa = RSA.Create(2048);
        File.WriteAllText(PrivatePath, rsa.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(PublicPath, rsa.ExportSubjectPublicKeyInfoPem());
        var identityConfig = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "Devkit.Tests", ["Jwt:Audience"] = "Devkit.Tests.Client",
            ["Jwt:SigningPrivateKeyPath"] = PrivatePath,
            ["Registration:Enabled"] = registrationEnabled.ToString(),
            ["BootstrapAccount:Enabled"] = (bootstrapEnabled || builtInAdmin).ToString(),
            ["BootstrapAccount:UserName"] = builtInAdmin ? "admin" : "bootstrap-admin",
            ["BootstrapAccount:Email"] = builtInAdmin ? "admin@example.invalid" : "bootstrap@example.test",
            ["BootstrapAccount:Password"] = builtInAdmin ? "admin" : "BootstrapPassword123",
            ["Redis:ConnectionString"] = "localhost:6379,abortConnect=false,connectTimeout=100",
            ["BackgroundJobs:RefreshTokenCleanup:Enabled"] = "false"
        };
        foreach (var pair in identityConfiguration ?? new Dictionary<string, string?>()) identityConfig[pair.Key] = pair.Value;
        identity = new IdentityHost(this, identityConfig, failingCache, prepareDatabase);
        var businessConfig = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "Devkit.Tests", ["Jwt:Audience"] = "Devkit.Tests.Client",
            ["Jwt:ValidationPublicKeyPath"] = PublicPath,
            ["Identity:BaseUrl"] = "http://identity.test", ["Identity:RequireHttps"] = "false",
            ["Workspace:DataRoot"] = Path.Combine(DataRoot, "storage"), ["Workspace:MinimumFreeBytes"] = "0",
            ["Workspace:QdrantUrl"] = "http://qdrant.test"
        };
        foreach (var pair in businessConfiguration ?? new Dictionary<string, string?>()) businessConfig[pair.Key] = pair.Value;
        business = new BusinessHost(this, businessConfig);
    }

    public HttpClient CreateIdentityClient() => identity.CreateClient();
    public HttpClient CreateBusinessClient()
    {
        _ = identity.Services; // Seed the independent identity database before accepting business traffic.
        return business.CreateClient();
    }
    public HttpClient CreateClient()
    {
        _ = identity.Services;
        _ = business.Services;
        return new HttpClient(new GatewayHandler(identity.Server.CreateHandler(), business.Server.CreateHandler()))
        { BaseAddress = new Uri("http://localhost") };
    }

    private sealed class GatewayHandler(HttpMessageHandler identity, HttpMessageHandler business) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker identityInvoker = new(identity);
        private readonly HttpMessageInvoker businessInvoker = new(business);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var isIdentity = path.StartsWith("/api/v1/auth/") || path.StartsWith("/api/v1/identity/")
                || path.StartsWith("/api/v1/web/") || path.StartsWith("/api/v1/client/navigation/");
            return (isIdentity ? identityInvoker : businessInvoker).SendAsync(request, ct);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { identityInvoker.Dispose(); businessInvoker.Dispose(); }
            base.Dispose(disposing);
        }
    }

    private sealed class IdentityBoundaryHandler(DevkitApiFactory owner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            owner.IdentityRequests++;
            if (owner.IdentityOverride is not null) return await owner.IdentityOverride(request, ct);
            using var invoker = new HttpMessageInvoker(owner.identity.Server.CreateHandler());
            return await invoker.SendAsync(request, ct);
        }
    }
    private sealed class BoundaryClients(DevkitApiFactory owner) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => name == "identity"
            ? new HttpClient(new IdentityBoundaryHandler(owner)) { BaseAddress = new Uri("http://identity.test"), Timeout = TimeSpan.FromSeconds(2) }
            : owner.External.CreateClient(name);
    }

    private sealed class IdentityHost(DevkitApiFactory owner, Dictionary<string, string?> config, bool failingCache,
        Action<IdentityDbContext>? prepare) : WebApplicationFactory<Devkit.Identity.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(config));
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<DbContextOptions<IdentityDbContext>>();
                services.RemoveAll<IdentityDbContext>();
                services.RemoveAll<IDbContextOptionsConfiguration<IdentityDbContext>>();
                using (var setup = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
                    .UseSqlite($"Data Source={owner.IdentityDatabasePath};Pooling=False").Options))
                { setup.Database.EnsureCreated(); prepare?.Invoke(setup); }
                services.AddDbContext<IdentityDbContext>((provider, options) =>
                {
                    options.UseSqlite($"Data Source={owner.IdentityDatabasePath};Pooling=False");
                    options.AddInterceptors(provider.GetRequiredService<Devkit.Infrastructure.Persistence.AuditingSaveChangesInterceptor>());
                });
                services.RemoveAll<IDistributedCache>();
                if (failingCache) services.AddSingleton<IDistributedCache, ThrowingDistributedCache>();
                else services.AddDistributedMemoryCache();
            });
        }
    }
    private sealed class BusinessHost(DevkitApiFactory owner, Dictionary<string, string?> config)
        : WebApplicationFactory<Devkit.Business.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(config));
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                foreach (var hosted in services.Where(d => d.ServiceType == typeof(IHostedService)
                    && d.ImplementationType == typeof(WorkspaceWorker)).ToArray()) services.Remove(hosted);
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(new BoundaryClients(owner));
                services.RemoveAll<IModelGateway>(); services.AddSingleton<IModelGateway>(owner.Model);
                services.RemoveAll<DbContextOptions<BusinessDbContext>>(); services.RemoveAll<BusinessDbContext>();
                services.RemoveAll<IDbContextOptionsConfiguration<BusinessDbContext>>();
                using (var setup = new BusinessDbContext(new DbContextOptionsBuilder<BusinessDbContext>()
                    .UseSqlite($"Data Source={owner.BusinessDatabasePath};Pooling=False").Options)) setup.Database.EnsureCreated();
                services.AddDbContext<BusinessDbContext>((provider, options) =>
                {
                    options.UseSqlite($"Data Source={owner.BusinessDatabasePath};Pooling=False");
                    options.AddInterceptors(provider.GetRequiredService<Devkit.Infrastructure.Persistence.AuditingSaveChangesInterceptor>());
                });
            });
        }
    }

    public async Task RunJobAsync(Guid jobId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
        var job = await db.Set<Devkit.Business.Domain.Workspace.WorkJob>().SingleAsync(x => x.Id == jobId);
        await scope.ServiceProvider.GetRequiredService<WorkspaceJobHandler>().RunAsync(job, CancellationToken.None);
        job.Status = "completed"; job.Progress = 100; await db.SaveChangesAsync();
    }
    public async Task InitializeStorageAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<LocalFileService>().EnsureDefaultAsync(CancellationToken.None);
    }
    public void Dispose()
    {
        business.Dispose(); identity.Dispose(); External.Dispose();
        var resolved = Path.GetFullPath(DataRoot);
        if (!resolved.StartsWith(testRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith("devkit-tests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove a directory outside this test's storage.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        private static InvalidOperationException Unavailable() => new("Isolated test cache unavailable");
        public byte[]? Get(string key) => throw Unavailable();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromException<byte[]?>(Unavailable());
        public void Refresh(string key) => throw Unavailable();
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.FromException(Unavailable());
        public void Remove(string key) => throw Unavailable();
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.FromException(Unavailable());
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Unavailable();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.FromException(Unavailable());
    }
}
