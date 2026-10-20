using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Devkit.Business.Infrastructure.Identity;
using Devkit.Business.Infrastructure.Persistence;
using Devkit.Identity.Application.Contracts.Auth;
using Devkit.Identity.Infrastructure.Identity;
using Devkit.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class ServerSeparationTests
{
    [Fact]
    public async Task Independent_hosts_expose_only_their_own_endpoints_and_database_models()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var identity = factory.CreateIdentityClient(); using var business = factory.CreateBusinessClient();
        var tokens = await Login(identity);
        business.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await business.GetAsync("/api/v1/exam-study/knowledge-bases")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await business.GetAsync("/api/v1/identity/users")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await business.GetAsync("/api/v1/web/menu-management/menus")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await identity.GetAsync("/api/v1/files/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await identity.GetAsync("/api/v1/system/info")).StatusCode);
        var identityPaths = (await identity.GetFromJsonAsync<JsonElement>("/openapi/v1.json")).GetProperty("paths");
        var businessPaths = (await business.GetFromJsonAsync<JsonElement>("/openapi/v1.json")).GetProperty("paths");
        Assert.True(identityPaths.TryGetProperty("/api/v1/auth/login", out _));
        Assert.True(identityPaths.TryGetProperty("/api/v1/web/menu-management/menus", out _));
        Assert.False(businessPaths.TryGetProperty("/api/v1/auth/login", out _));
        Assert.False(businessPaths.TryGetProperty("/api/v1/identity/users", out _));
        using var identityScope = factory.IdentityServices.CreateScope(); using var businessScope = factory.Services.CreateScope();
        var identityDb = identityScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var businessDb = businessScope.ServiceProvider.GetRequiredService<BusinessDbContext>();
        Assert.NotEqual(identityDb.Database.GetConnectionString(), businessDb.Database.GetConnectionString());
        Assert.DoesNotContain(businessDb.Model.GetEntityTypes(), type => type.GetTableName() is "Users" or "Roles" or "RefreshTokens" or "WebMenus");
        Assert.DoesNotContain(identityDb.Model.GetEntityTypes(), type => type.GetTableName() is "StoredFiles" or "StudyProjects" or "WorkspaceJobs" or "ModuleInstances");
        Assert.Contains(identityDb.Database.GetMigrations(), name => name.EndsWith("InitialIdentityContext"));
        Assert.Contains(businessDb.Database.GetMigrations(), name => name.EndsWith("InitialBusinessContext"));
        foreach (var assembly in new[] { typeof(Devkit.Business.Api.Program).Assembly, typeof(BusinessDbContext).Assembly,
            typeof(Devkit.Business.Application.Workspace.Actor).Assembly, typeof(Devkit.Business.Domain.Workspace.KnowledgeBase).Assembly })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), dependency => dependency.Name!.StartsWith("Devkit.Identity."));
        foreach (var assembly in new[] { typeof(Devkit.Identity.Api.Program).Assembly, typeof(IdentityDbContext).Assembly,
            typeof(TokenPairDto).Assembly, typeof(Devkit.Identity.Domain.Identity.User).Assembly })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), dependency => dependency.Name!.StartsWith("Devkit.Business."));
        var publicKey = (RsaSecurityKey)factory.Services.GetRequiredService<JwtValidationKey>().ValidationKey;
        Assert.ThrowsAny<CryptographicException>(() => publicKey.Rsa.ExportParameters(includePrivateParameters: true));
    }

    [Theory]
    [InlineData("logout-all")]
    [InlineData("refresh-replay")]
    [InlineData("disable")]
    [InlineData("password")]
    public async Task Revocation_at_identity_invalidates_business_requests_without_shared_identity_storage(string action)
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var admin = factory.CreateIdentityClient();
        await Login(admin);
        var profile = await Data<Devkit.Identity.Application.Contracts.Auth.UserProfileDto>(await admin.PostAsJsonAsync("/api/v1/auth/register", new
        { userName = "learner", email = "learner@example.test", password = "StrongPassword123" }), HttpStatusCode.Created);
        using var learner = factory.CreateIdentityClient(); var tokens = await Login(learner, "learner", "StrongPassword123");
        using var business = factory.CreateBusinessClient(); business.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        await Problem(await business.GetAsync("/api/v1/exam-study/knowledge-bases"), HttpStatusCode.Forbidden, "forbidden");
        if (action == "logout-all") Assert.Equal(HttpStatusCode.NoContent, (await learner.PostAsync("/api/v1/auth/logout-all", null)).StatusCode);
        else if (action == "disable") (await admin.DeleteAsync($"/api/v1/identity/users/{profile.Id}")).EnsureSuccessStatusCode();
        else if (action == "password") (await learner.PostAsJsonAsync("/api/v1/auth/password", new { currentPassword = "StrongPassword123", newPassword = "ChangedPassword123" })).EnsureSuccessStatusCode();
        else
        {
            var fresh = await Data<TokenPairDto>(await learner.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken }));
            business.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fresh.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, (await learner.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken })).StatusCode);
        }
        await Problem(await business.GetAsync("/api/v1/exam-study/knowledge-bases"), HttpStatusCode.Unauthorized, "unauthorized");
    }

    [Fact]
    public async Task Business_reads_identity_once_per_request_and_never_uses_a_stale_assertion()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var identity = factory.CreateIdentityClient();
        var pair = await Login(identity); using var business = factory.CreateBusinessClient();
        business.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        var before = factory.IdentityRequests;
        Assert.Equal(HttpStatusCode.OK, (await business.GetAsync("/api/v1/exam-study/knowledge-bases")).StatusCode);
        Assert.Equal(before + 1, factory.IdentityRequests);
        Assert.Equal(HttpStatusCode.OK, (await business.GetAsync("/api/v1/exam-study/knowledge-bases")).StatusCode);
        Assert.Equal(before + 2, factory.IdentityRequests);
    }

    [Theory]
    [InlineData("network")]
    [InlineData("timeout")]
    [InlineData("unavailable")]
    [InlineData("redirect")]
    [InlineData("malformed")]
    [InlineData("wrong-subject")]
    [InlineData("missing-permissions")]
    public async Task Untrusted_or_unavailable_identity_assertions_fail_closed_without_invalidating_client_session(string failure)
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var identity = factory.CreateIdentityClient();
        var pair = await Login(identity); using var business = factory.CreateBusinessClient();
        business.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        factory.IdentityOverride = (_, _) => failure switch
        {
            "network" => throw new HttpRequestException("test unavailable"),
            "timeout" => throw new TaskCanceledException("test timeout"),
            "unavailable" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            "redirect" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)),
            "malformed" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid-json") }),
            "wrong-subject" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { data = new { id = Guid.NewGuid(), administrator = true, permissions = Array.Empty<string>(), menuCodes = Array.Empty<string>() } }) }),
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { data = new { id = pair.User.Id, administrator = true } }) })
        };
        await Problem(await business.GetAsync("/api/v1/exam-study/knowledge-bases"), HttpStatusCode.ServiceUnavailable, "identity_unavailable");
        factory.IdentityOverride = null;
        Assert.Equal(HttpStatusCode.OK, (await business.GetAsync("/api/v1/exam-study/knowledge-bases")).StatusCode);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("algorithm")]
    [InlineData("none")]
    public async Task Business_validates_JWT_before_sending_credentials_to_identity(string defect)
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var identity = factory.CreateIdentityClient();
        var issued = new JwtSecurityTokenHandler().ReadJwtToken((await Login(identity)).AccessToken);
        using var business = factory.CreateBusinessClient(); using var attacker = RSA.Create(2048);
        var credentials = defect == "algorithm" ? new SigningCredentials(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)), SecurityAlgorithms.HmacSha256)
            : new SigningCredentials(defect == "signature" ? new RsaSecurityKey(attacker) : factory.IdentityServices.GetRequiredService<JwtKeyMaterial>().ValidationKey, SecurityAlgorithms.RsaSha256);
        var jwt = new JwtSecurityToken(defect == "issuer" ? "wrong" : "Devkit.Tests", defect == "audience" ? "wrong" : "Devkit.Tests.Client",
            issued.Claims.Where(claim => claim.Type is not ("iss" or "aud" or "exp" or "nbf" or "iat")),
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(defect == "expired" ? -5 : 5), credentials);
        business.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        var before = factory.IdentityRequests; var response = await business.GetAsync("/api/v1/exam-study/knowledge-bases");
        if (defect == "none") { Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(before + 1, factory.IdentityRequests); }
        else { await Problem(response, HttpStatusCode.Unauthorized, "unauthorized"); Assert.Equal(before, factory.IdentityRequests); }
    }

    [Theory]
    [InlineData("private-in-public")]
    [InlineData("missing-public")]
    [InlineData("weak-public")]
    public void Business_rejects_unsuitable_public_keys(string defect)
    {
        using var factory = new DevkitApiFactory();
        if (defect == "private-in-public") File.WriteAllText(factory.PublicPath, File.ReadAllText(factory.PrivatePath));
        else if (defect == "missing-public") File.Delete(factory.PublicPath);
        else { using var weak = RSA.Create(1024); File.WriteAllText(factory.PublicPath, weak.ExportSubjectPublicKeyInfoPem()); }
        Assert.ThrowsAny<Exception>(() => factory.CreateBusinessClient());
    }
}
