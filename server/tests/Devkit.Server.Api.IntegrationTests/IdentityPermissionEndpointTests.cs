using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Devkit.Server.Application.Contracts.Auth;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.Workspace;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class IdentityPermissionEndpointTests
{
    [Theory]
    [InlineData("username", "invalid_account")]
    [InlineData("permissions", "invalid_role")]
    [InlineData("roles", "invalid_role")]
    [InlineData("password", "invalid_password")]
    public async Task Identity_null_fields_return_validation_problems(string field, string code)
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var client = factory.CreateClient(); var admin = await Login(client);
        var response = field switch
        {
            "username" => await client.PostAsJsonAsync("/api/v1/identity/users", new AccountRequest(null!, "missing@example.test", "StrongPassword123", [])),
            "permissions" => await client.PostAsJsonAsync("/api/v1/identity/roles", new RoleRequest("角色", null!)),
            "roles" => await client.PutAsJsonAsync($"/api/v1/identity/users/{admin.User.Id}/roles", new AssignRolesRequest(null!)),
            _ => await client.PostAsJsonAsync("/api/v1/auth/password", new ChangePasswordRequest(null!, null!))
        };
        await Problem(response, HttpStatusCode.BadRequest, code);
    }

    [Theory]
    [InlineData("/api/v1/exam-study/knowledge-bases")]
    [InlineData("/api/v1/exam-study/questions")]
    [InlineData("/api/v1/files/")]
    [InlineData("/api/v1/storage/locations")]
    [InlineData("/api/v1/identity/users")]
    public async Task Protected_endpoints_require_login(string url)
    {
        using var factory = new DevkitApiFactory(); using var client = factory.CreateClient();
        await Problem(await client.GetAsync(url), HttpStatusCode.Unauthorized, "unauthorized");
        var menus = await Data<JsonElement>(await client.GetAsync("/api/v1/web/navigation/menus"));
        Assert.DoesNotContain("study-knowledge", menus.ToString());
    }

    [Fact]
    public async Task Default_admin_has_all_permissions_and_last_admin_is_protected()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var client = factory.CreateClient();
        var pair = await Login(client); var actor = await Data<Actor>(await client.GetAsync("/api/v1/auth/permissions"));
        Assert.True(actor.Administrator); Assert.Equal(PermissionCatalog.All.Order(), actor.Permissions.Order());
        var menus = await Data<JsonElement>(await client.GetAsync("/api/v1/web/navigation/menus"));
        Assert.Contains("study-knowledge", menus.ToString()); Assert.Contains("system-storage", menus.ToString());
        await Problem(await client.DeleteAsync($"/api/v1/identity/users/{pair.User.Id}"), HttpStatusCode.Conflict, "last_administrator");
        await Problem(await client.PutAsJsonAsync($"/api/v1/identity/users/{pair.User.Id}/roles", new AssignRolesRequest([])), HttpStatusCode.Conflict, "last_administrator");
    }

    [Fact]
    public async Task Menu_permission_is_required_and_grant_and_revocation_take_effect()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var admin = factory.CreateClient(); await Login(admin);
        using var user = factory.CreateClient();
        var registered = await Data<UserProfileDto>(await user.PostAsJsonAsync("/api/v1/auth/register", new { userName = "learner", email = "learner@example.test", password = "StrongPassword123" }), HttpStatusCode.Created);
        await Login(user, "learner", "StrongPassword123");
        await Problem(await user.GetAsync("/api/v1/exam-study/knowledge-bases"), HttpStatusCode.Forbidden, "forbidden");
        Assert.DoesNotContain("study-knowledge", (await Data<JsonElement>(await user.GetAsync("/api/v1/web/navigation/menus"))).ToString());
        var role = await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/roles", new RoleRequest("制度学习者", ["exam-study.access"])));
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/users/{registered.Id}/roles", new AssignRolesRequest([role])));
        await Problem(await user.GetAsync("/api/v1/exam-study/knowledge-bases"), HttpStatusCode.Unauthorized, "unauthorized");
        await Login(user, "learner", "StrongPassword123");
        Assert.Contains("study-knowledge", (await Data<JsonElement>(await user.GetAsync("/api/v1/web/navigation/menus"))).ToString());
        await Base(user);
        await Data<Guid>(await admin.PutAsJsonAsync($"/api/v1/identity/roles/{role}", new RoleRequest("制度学习者", [])));
        await Problem(await user.GetAsync("/api/v1/exam-study/knowledge-bases"), HttpStatusCode.Forbidden, "forbidden");
        Assert.DoesNotContain("study-knowledge", (await Data<JsonElement>(await user.GetAsync("/api/v1/web/navigation/menus"))).ToString());
    }

    [Fact]
    public async Task Ordinary_users_cannot_read_or_link_another_owners_resources_but_admin_can()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var admin = factory.CreateClient(); await Login(admin);
        await StudyUser(admin, "alice"); await StudyUser(admin, "bobby");
        using var alice = factory.CreateClient(); using var bob = factory.CreateClient();
        await Login(alice, "alice", "StrongPassword123"); await Login(bob, "bobby", "StrongPassword123");
        var kb = await Base(alice); var q = await ApprovedQuestion(alice, kb.Id); var project = await Project(alice, kb.Id);
        var attempt = await Data<AttemptView>(await alice.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/attempts", new AttemptRequest(Count: 1)));
        Assert.Empty(await Data<KnowledgeBase[]>(await bob.GetAsync("/api/v1/exam-study/knowledge-bases?all=true")));
        await Problem(await bob.GetAsync($"/api/v1/exam-study/knowledge-bases/{kb.Id}/sources"), HttpStatusCode.NotFound, "not_found");
        await Problem(await bob.GetAsync($"/api/v1/exam-study/attempts/{attempt.Id}"), HttpStatusCode.NotFound, "not_found");
        await Problem(await bob.PostAsJsonAsync("/api/v1/exam-study/projects", new ProjectRequest("越权关联", "", null, [kb.Id])), HttpStatusCode.NotFound, "not_found");
        Assert.Single(await Data<KnowledgeBase[]>(await admin.GetAsync("/api/v1/exam-study/knowledge-bases?all=true")));
        Assert.Equal(attempt.Id, (await Data<AttemptView>(await admin.GetAsync($"/api/v1/exam-study/attempts/{attempt.Id}"))).Id);
    }

    [Fact]
    public async Task Identity_manager_cannot_promote_self_to_administrator()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var admin = factory.CreateClient(); await Login(admin);
        var id = await StudyUser(admin, "manager", "system.identity.manage");
        var administrator = (await Data<RoleView[]>(await admin.GetAsync("/api/v1/identity/roles"))).Single(x => x.Name == "Administrator");
        using var manager = factory.CreateClient(); await Login(manager, "manager", "StrongPassword123");
        await Problem(await manager.PutAsJsonAsync($"/api/v1/identity/users/{id}/roles", new AssignRolesRequest([administrator.Id])), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await manager.PostAsJsonAsync("/api/v1/identity/users", new AccountRequest("new-admin", "new-admin@example.test", "StrongPassword123", [administrator.Id])), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Changing_password_invalidates_access_and_refresh_tokens_without_resetting_admin()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var client = factory.CreateClient(); var pair = await Login(client);
        await Problem(await client.PostAsJsonAsync("/api/v1/auth/password", new ChangePasswordRequest("wrong", "ChangedPassword123")), HttpStatusCode.BadRequest, "invalid_password");
        await Data<SuccessView>(await client.PostAsJsonAsync("/api/v1/auth/password", new ChangePasswordRequest("admin", "ChangedPassword123")));
        await Problem(await client.GetAsync("/api/v1/auth/permissions"), HttpStatusCode.Unauthorized, "unauthorized");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = pair.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { account = "admin", password = "admin" })).StatusCode);
        await Login(client, "admin", "ChangedPassword123");
        Assert.True((await Data<Actor>(await client.GetAsync("/api/v1/auth/permissions"))).Administrator);
    }
}
