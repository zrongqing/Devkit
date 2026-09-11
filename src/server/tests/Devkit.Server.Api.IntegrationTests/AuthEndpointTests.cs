using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Devkit.Server.Application.Contracts.Auth;
using Devkit.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class AuthEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Authentication_flow_rotates_and_revokes_tokens()
    {
        using var factory = new DevkitApiFactory();
        using var client = factory.CreateClient();

        var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            userName = "alice",
            email = "alice@example.test",
            password = "StrongPassword123"
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var registrationJson = await registration.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", registrationJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokenHash", registrationJson, StringComparison.OrdinalIgnoreCase);
        var registered = await ReadDataAsync<UserProfileDto>(registration);
        Assert.Equal(7, registered.Id.Version);
        Assert.Contains("User", registered.Roles);

        var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            userName = "alice",
            email = "alice@example.test",
            password = "StrongPassword123"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            account = "ALICE@EXAMPLE.TEST",
            password = "StrongPassword123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var firstPair = await ReadDataAsync<TokenPairDto>(login);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstPair.AccessToken);
        var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = firstPair.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var secondPair = await ReadDataAsync<TokenPairDto>(refresh);
        Assert.NotEqual(firstPair.RefreshToken, secondPair.RefreshToken);

        client.DefaultRequestHeaders.Authorization = null;
        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = secondPair.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var refreshAfterLogout = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = secondPair.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondPair.AccessToken);
        var logoutAll = await client.PostAsync("/api/v1/auth/logout-all", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutAll.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevkitDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.Id == registered.Id);
        Assert.NotEqual(default, user.CreatedAtUtc);
        Assert.Equal(user.Id, user.CreatedBy);

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync();
        Assert.False(await dbContext.Users.AnyAsync(item => item.Id == user.Id));
        Assert.True((await dbContext.Users.IgnoreQueryFilters().SingleAsync(item => item.Id == user.Id)).IsDeleted);
    }

    [Fact]
    public async Task Reusing_rotated_token_invalidates_current_access_token()
    {
        using var factory = new DevkitApiFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "bob", "bob@example.test");
        var firstPair = await LoginAsync(client, "bob");
        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = firstPair.RefreshToken });
        var secondPair = await ReadDataAsync<TokenPairDto>(refreshed);

        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = firstPair.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondPair.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_locks_after_five_failures()
    {
        using var factory = new DevkitApiFactory();
        using var client = factory.CreateClient();
        await RegisterAsync(client, "carol", "carol@example.test");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await client.PostAsJsonAsync("/api/v1/auth/login", new { account = "carol", password = "WrongPassword123" });
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var locked = await client.PostAsJsonAsync("/api/v1/auth/login", new { account = "carol", password = "StrongPassword123" });
        Assert.Equal((HttpStatusCode)423, locked.StatusCode);
    }

    [Fact]
    public async Task Registration_can_be_disabled()
    {
        using var factory = new DevkitApiFactory(registrationEnabled: false);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            userName = "dave",
            email = "dave@example.test",
            password = "StrongPassword123"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("registration_disabled", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Bootstrap_account_receives_administrator_role()
    {
        using var factory = new DevkitApiFactory(registrationEnabled: false, bootstrapEnabled: true);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            account = "bootstrap-admin",
            password = "BootstrapPassword123"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pair = await ReadDataAsync<TokenPairDto>(response);
        Assert.Contains("Administrator", pair.User.Roles);
    }

    [Fact]
    public async Task Redis_failure_falls_back_to_database()
    {
        using var factory = new DevkitApiFactory(failingCache: true);
        using var client = factory.CreateClient();
        await RegisterAsync(client, "erin", "erin@example.test");
        var pair = await LoginAsync(client, "erin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task OpenApi_contains_versioned_authentication_contracts()
    {
        using var factory = new DevkitApiFactory();
        using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", JsonOptions);
        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/auth/login", out _));
        Assert.True(paths.TryGetProperty("/api/v1/auth/refresh", out _));
        Assert.True(paths.TryGetProperty("/api/v1/auth/me", out _));
    }

    private static async Task RegisterAsync(HttpClient client, string userName, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            userName,
            email,
            password = "StrongPassword123"
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<TokenPairDto> LoginAsync(HttpClient client, string account)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { account, password = "StrongPassword123" });
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<TokenPairDto>(response);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return json.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }
}
