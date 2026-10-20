using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Devkit.AspNetCore.Contracts;
using Devkit.Commons.Exceptions;
using Devkit.DomainCommons.Entities;
using Devkit.Infrastructure.Persistence;
using Devkit.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class CommonInfrastructureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_auditing_preserves_creation_history_tracks_updates_and_soft_deletes(bool asynchronous)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var creator = Guid.NewGuid(); var editor = Guid.NewGuid();
        var clock = new AuditClock();
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", creator.ToString())], "test"));
        var options = new DbContextOptionsBuilder<AuditContext>().UseSqlite(connection)
            .AddInterceptors(new AuditingSaveChangesInterceptor(clock, http)).Options;
        await using var db = new AuditContext(options);
        await db.Database.EnsureCreatedAsync();
        var row = new AuditRow { Name = "created" }; db.Add(row);
        await Persist();
        var createdAt = row.CreatedAtUtc;
        Assert.Equal(creator, row.CreatedBy); Assert.Equal(clock.Utc.UtcDateTime, createdAt);

        clock.Utc = clock.Utc.AddMinutes(1);
        http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, editor.ToString())], "test"));
        row.Name = "edited"; await Persist();
        Assert.Equal(creator, row.CreatedBy); Assert.Equal(createdAt, row.CreatedAtUtc);
        Assert.Equal(editor, row.UpdatedBy); Assert.Equal(clock.Utc.UtcDateTime, row.UpdatedAtUtc);

        clock.Utc = clock.Utc.AddMinutes(1);
        db.Remove(row); await Persist();
        Assert.False(await db.Set<AuditRow>().AnyAsync());
        var retained = await db.Set<AuditRow>().IgnoreQueryFilters().SingleAsync();
        Assert.True(retained.IsDeleted); Assert.Equal(creator, retained.CreatedBy);
        Assert.Equal(editor, retained.UpdatedBy); Assert.Equal(createdAt, retained.CreatedAtUtc);
        Assert.Equal(clock.Utc.UtcDateTime, retained.UpdatedAtUtc);

        // Imported history remains owned by the importing context, not replaced by the current caller.
        var imported = new AuditRow { Name = "imported", CreatedBy = creator, CreatedAtUtc = createdAt.AddYears(-1) };
        db.Add(imported); await Persist();
        Assert.Equal(creator, imported.CreatedBy); Assert.Equal(createdAt.AddYears(-1), imported.CreatedAtUtc);

        async Task Persist()
        {
            if (asynchronous) await db.SaveChangesAsync();
            else db.SaveChanges();
        }
    }

    [Fact]
    public void Shared_RSA_loader_keeps_matching_key_ids_and_requires_private_material_only_for_issuance()
    {
        using var storage = new DevkitApiFactory();
        using var issuer = RsaKeyLoader.ReadPrivateKey(storage.PrivatePath);
        using var verifier = RsaKeyLoader.ReadPublicKey(storage.PublicPath);
        Assert.Equal(RsaKeyLoader.CreateSecurityKey(issuer).KeyId, RsaKeyLoader.CreateSecurityKey(verifier).KeyId);
        var bytes = RandomNumberGenerator.GetBytes(48);
        var signature = issuer.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.True(verifier.VerifyData(bytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        Assert.ThrowsAny<CryptographicException>(() => verifier.ExportParameters(includePrivateParameters: true));
        Assert.ThrowsAny<CryptographicException>(() => RsaKeyLoader.ReadPrivateKey(storage.PublicPath));
        Assert.Throws<InvalidOperationException>(() => RsaKeyLoader.ReadPublicKey(storage.PrivatePath));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Shared_hosting_preserves_explicit_CORS_origins_for_each_service(bool identity)
    {
        var cors = new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "https://allowed.example.test" };
        using var factory = new DevkitApiFactory(identityConfiguration: cors, businessConfiguration: cors);
        using var client = identity ? factory.CreateIdentityClient() : factory.CreateBusinessClient();
        var path = identity ? "/api/v1/auth/login" : "/api/v1/exam-study/projects";
        using var allowed = Preflight("https://allowed.example.test");
        using var allowedResponse = await client.SendAsync(allowed);
        Assert.Equal(HttpStatusCode.NoContent, allowedResponse.StatusCode);
        Assert.Equal("https://allowed.example.test", Assert.Single(allowedResponse.Headers.GetValues("Access-Control-Allow-Origin")));
        using var denied = Preflight("https://denied.example.test");
        using var deniedResponse = await client.SendAsync(denied);
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));

        HttpRequestMessage Preflight(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, path);
            request.Headers.Add("Origin", origin); request.Headers.Add("Access-Control-Request-Method", "POST");
            return request;
        }
    }

    [Fact]
    public async Task Shared_health_routes_keep_liveness_independent_of_context_readiness()
    {
        using var factory = new DevkitApiFactory(); using var client = factory.CreateBusinessClient();
        factory.IdentityOverride = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(0, factory.IdentityRequests);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(2, factory.IdentityRequests); // Business and FileStorage independently check Identity.
        Assert.Equal(1, factory.FileRequests);
    }

    [Fact]
    public void Common_libraries_cannot_reference_either_bounded_context_and_domain_primitives_remain_framework_free()
    {
        foreach (var assembly in new[] { typeof(ServiceException).Assembly, typeof(AuditableEntity).Assembly,
            typeof(RsaKeyLoader).Assembly, typeof(ApiResponse<>).Assembly })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name!.StartsWith("Devkit.Identity.")
                || reference.Name.StartsWith("Devkit.Business."));
        Assert.DoesNotContain(typeof(AuditableEntity).Assembly.GetReferencedAssemblies(), reference =>
            reference.Name!.StartsWith("Microsoft.AspNetCore") || reference.Name.StartsWith("Microsoft.EntityFrameworkCore"));
    }

    private sealed class AuditClock : TimeProvider
    {
        public DateTimeOffset Utc { get; set; } = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Utc;
    }
    private sealed class AuditRow : AuditableEntity { public string Name { get; set; } = ""; }
    private sealed class AuditContext(DbContextOptions<AuditContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder) => builder.Entity<AuditRow>().HasQueryFilter(row => !row.IsDeleted);
    }
}
