using System.Net;
using System.Net.Http.Json;
using Devkit.Application.Application.Administration;
using Devkit.Application.Domain.Organization;
using Devkit.Commons.Runtime;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class AccountMaintenanceTests
{
    [Fact]
    public async Task Creating_user_persists_audit_and_failed_requests_do_not_create_users_or_events()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var admin = factory.CreateIdentityClient();
        using var user = factory.CreateIdentityClient();
        const string users = "/api/v1/identity/users";
        const string audit = "/api/v1/identity/audit";
        var request = new AccountRequest("new-worker", "new-worker@example.test", "StrongPassword123", []);
        await Problem(await user.PostAsJsonAsync(users, request), HttpStatusCode.Unauthorized, "unauthorized");
        var actor = await Login(admin);
        await Problem(await admin.PostAsJsonAsync(users, request with { Password = "weak" }), HttpStatusCode.BadRequest, "invalid_account");
        await Problem(await admin.PostAsJsonAsync(users, request with { RoleIds = [Guid.NewGuid()] }), HttpStatusCode.BadRequest, "invalid_role");
        Assert.Empty(await Data<AdministrationEvent[]>(await admin.GetAsync(audit)));

        var id = await Data<Guid>(await admin.PostAsJsonAsync(users, request));
        var created = Assert.Single(await Data<AccountView[]>(await admin.GetAsync(users)), x => x.Id == id);
        Assert.Equal(request.UserName, created.UserName);
        Assert.Equal(request.Email, created.Email);
        Assert.Empty(created.Roles);
        await Login(user, request.UserName, request.Password);
        await Problem(await user.PostAsJsonAsync(users, request with { UserName = "forbidden-worker" }), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await admin.PostAsJsonAsync(users, request), HttpStatusCode.Conflict, "account_exists");
        var events = await Data<AdministrationEvent[]>(await admin.GetAsync(audit));
        var activity = Assert.Single(events);
        Assert.Equal(actor.User.Id, activity.ActorId);
        Assert.Equal("POST " + users, activity.Operation);
        Assert.False(string.IsNullOrWhiteSpace(activity.TraceId));
        Assert.DoesNotContain(request.Password, System.Text.Json.JsonSerializer.Serialize(events));
        Assert.Equal(2, (await Data<AccountView[]>(await admin.GetAsync(users))).Length);
    }

    [Fact]
    public async Task Restoring_requires_admin_and_old_sessions_stay_revoked()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var admin = factory.CreateIdentityClient();
        using var user = factory.CreateIdentityClient();
        const string disabled = "/api/v1/identity/users/disabled";
        const string runtime = "/api/v1/identity/runtime";
        await Problem(await user.GetAsync(disabled), HttpStatusCode.Unauthorized, "unauthorized");
        await Problem(await user.GetAsync(runtime), HttpStatusCode.Unauthorized, "unauthorized");
        await Login(admin);
        var id = await StudyUser(admin, "worker");
        var old = await Login(user, "worker", "StrongPassword123");
        await Problem(await user.GetAsync(disabled), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await user.GetAsync(runtime), HttpStatusCode.Forbidden, "forbidden");
        var rotated = await Data<Devkit.Application.Application.Contracts.Auth.TokenPairDto>(
            await user.PostAsJsonAsync("/api/v1/auth/refresh", new { old.RefreshToken }));
        var resource = $"/api/v1/identity/users/{id}";
        await admin.DeleteAsync(resource);
        Assert.Equal(id, Assert.Single(await Data<DisabledAccountView[]>(await admin.GetAsync(disabled))).Id);
        await StudyUser(admin, "supervisor", "system.users.manage", "system.monitor.view");
        using var manager = factory.CreateIdentityClient();
        await Login(manager, "supervisor", "StrongPassword123");
        await Problem(await manager.PostAsync(resource + "/restore", null), HttpStatusCode.Forbidden, "forbidden");
        var sample = await Data<SystemRuntimeResponse>(await manager.GetAsync(runtime));
        Assert.Equal("Devkit Application", sample.Info.ServiceName);
        Assert.True(sample.WorkingSetBytes > 0);
        await Problem(await admin.PostAsync($"/api/v1/identity/users/{Guid.NewGuid()}/restore", null), HttpStatusCode.NotFound, "not_found");
        (await admin.PostAsync(resource + "/restore", null)).EnsureSuccessStatusCode();
        await Problem(await admin.PostAsync(resource + "/restore", null), HttpStatusCode.Conflict, "account_active");
        await Problem(await user.GetAsync("/api/v1/auth/permissions"), HttpStatusCode.Unauthorized, "unauthorized");
        await Problem(await user.PostAsJsonAsync("/api/v1/auth/refresh", new { old.RefreshToken }), HttpStatusCode.Unauthorized, "invalid_refresh_token");
        await Login(user, "worker", "StrongPassword123");
        await Problem(await user.PostAsJsonAsync("/api/v1/auth/refresh", new { rotated.RefreshToken }), HttpStatusCode.Unauthorized, "invalid_refresh_token");
        (await user.GetAsync("/api/v1/auth/permissions")).EnsureSuccessStatusCode();
        Assert.Empty(await Data<DisabledAccountView[]>(await admin.GetAsync(disabled)));
    }
}
