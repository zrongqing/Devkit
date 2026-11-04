using System.Net;
using System.Net.Http.Json;
using Devkit.Application.Application.Administration;
using Devkit.Commons.Runtime;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class AccountMaintenanceTests
{
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
