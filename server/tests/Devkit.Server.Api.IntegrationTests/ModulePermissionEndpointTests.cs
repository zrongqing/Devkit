using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.Workspace;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class ModulePermissionEndpointTests
{
    [Fact]
    public async Task Runtime_metrics_require_monitor_permission_and_report_process_state()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var user = factory.CreateClient();
        await Problem(await user.GetAsync("/api/v1/system/runtime"), HttpStatusCode.Unauthorized, "unauthorized");
        using var admin = factory.CreateClient(); await Login(admin);
        var id = await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/users", new AccountRequest("monitor", "monitor@example.test", "StrongPassword123", [])));
        await Login(user, "monitor", "StrongPassword123");
        await Problem(await user.GetAsync("/api/v1/system/runtime"), HttpStatusCode.Forbidden, "forbidden");
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/users/{id}/permissions", new AssignPermissionsRequest(["system.monitor.view"])));
        var runtime = await Data<Devkit.Server.Application.Contracts.SystemRuntimeResponse>(await user.GetAsync("/api/v1/system/runtime"));
        Assert.True(runtime.UptimeSeconds >= 0); Assert.True(runtime.WorkingSetBytes > 0);
        Assert.True(runtime.ThreadCount > 0); Assert.True(runtime.ProcessorCount > 0);
        Assert.True(runtime.StartedAtUtc <= runtime.Info.ServerTime);
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/users/{id}/menus", new { menuCodes = new[] { "system.monitor.status" } }));
        Assert.Contains("system-status", (await Data<JsonElement>(await user.GetAsync("/api/v1/web/navigation/menus"))).ToString());
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/users/{id}/permissions", new AssignPermissionsRequest([])));
        await Problem(await user.GetAsync("/api/v1/system/runtime"), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Direct_and_role_grants_are_unioned_and_revoked_on_each_request()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var admin = factory.CreateClient(); await Login(admin);
        var role = await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/roles", new RoleRequest("检索角色", ["study.search"])));
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/roles/{role}/menus", new { menuCodes = new[] { "knowledge.search" } }));
        var userId = await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/users", new AccountRequest("reader", "reader@example.test", "StrongPassword123", [role])));
        using var user = factory.CreateClient(); await Login(user, "reader", "StrongPassword123");
        var menus = (await Data<JsonElement>(await user.GetAsync("/api/v1/web/navigation/menus"))).ToString();
        Assert.Contains("study-projects", menus); Assert.DoesNotContain("study-project-management", menus);
        await Data<StudyProject[]>(await user.GetAsync("/api/v1/exam-study/projects"));
        await Problem(await user.PostAsJsonAsync("/api/v1/exam-study/projects", new ProjectRequest("项目", "", null, [])), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await user.GetAsync("/api/v1/exam-study/questions"), HttpStatusCode.Forbidden, "forbidden");
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/users/{userId}/permissions", new AssignPermissionsRequest(["study.projects.manage", "study.search"])));
        var project = await Project(user);
        var view = (await Data<AccountView[]>(await admin.GetAsync("/api/v1/identity/users"))).Single(x => x.Id == userId);
        Assert.Equal(2, view.Permissions.Length); Assert.Contains("study.search", view.EffectivePermissions);
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/users/{userId}/menus", new { menuCodes = new[] { "knowledge.projects" } }));
        Assert.Contains("study-project-management", (await Data<JsonElement>(await user.GetAsync("/api/v1/web/navigation/menus"))).ToString());
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/users/{userId}/permissions", new AssignPermissionsRequest([])));
        await Problem(await user.PutAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}", new ProjectRequest("无权修改", "", null, [], project.Revision)), HttpStatusCode.Forbidden, "forbidden");
        Assert.Contains("study.search", (await Data<Actor>(await user.GetAsync("/api/v1/auth/permissions"))).Permissions);
        await Data<Guid>(await admin.PutAsJsonAsync($"/api/v1/identity/roles/{role}", new RoleRequest("检索角色", [])));
        await Problem(await user.GetAsync("/api/v1/exam-study/projects"), HttpStatusCode.Forbidden, "forbidden");
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/roles/{role}/menus", new { menuCodes = Array.Empty<string>() }));
        Assert.DoesNotContain("study-projects", (await Data<JsonElement>(await user.GetAsync("/api/v1/web/navigation/menus"))).ToString());
    }

    [Fact]
    public async Task Practice_module_can_read_progress_and_answer_without_question_management()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var admin = factory.CreateClient(); await Login(admin);
        var id = await StudyUser(admin, "student");
        using var student = factory.CreateClient(); await Login(student, "student", "StrongPassword123");
        var kb = await Base(student); var question = await ApprovedQuestion(student, kb.Id); var project = await Project(student, kb.Id);
        var role = (await Data<RoleView[]>(await admin.GetAsync("/api/v1/identity/roles"))).Single(x => x.Name == "role-student");
        await Data<Guid>(await admin.PutAsJsonAsync($"/api/v1/identity/roles/{role.Id}", new RoleRequest(role.Name, ["study.practice"])));
        var attempt = await Data<AttemptView>(await student.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/attempts", new AttemptRequest("practice", Count: 1)));
        await Data<AttemptView>(await student.PutAsJsonAsync($"/api/v1/exam-study/attempts/{attempt.Id}/answers/{question.Id}", new AnswerRequest(["B"], attempt.Revision)));
        var progress = await Data<ProgressView>(await student.GetAsync($"/api/v1/exam-study/projects/{project.Id}/progress"));
        Assert.Single(progress.Mistakes); Assert.Equal(question.Stem, progress.QuestionNames[question.Id]);
        await Problem(await student.GetAsync("/api/v1/exam-study/questions"), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await student.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/search", new SearchRequest("请假")), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await student.PostAsJsonAsync("/api/v1/exam-study/knowledge-bases", new NamedRequest("越权")), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task User_profile_update_validates_unique_identity_and_missing_accounts()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var admin = factory.CreateClient(); await Login(admin);
        var id = await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/users", new AccountRequest("editor", "editor@example.test", "StrongPassword123", [])));
        await Data<SuccessView>(await admin.PutAsJsonAsync($"/api/v1/identity/users/{id}", new AccountUpdateRequest("updated", "updated@example.test")));
        var profile = (await Data<AccountView[]>(await admin.GetAsync("/api/v1/identity/users"))).Single(x => x.Id == id);
        Assert.Equal("updated", profile.UserName); Assert.Equal("updated@example.test", profile.Email);
        using var user = factory.CreateClient(); await Login(user, "updated", "StrongPassword123");
        await Problem(await admin.PutAsJsonAsync($"/api/v1/identity/users/{id}", new AccountUpdateRequest("admin", "updated@example.test")), HttpStatusCode.Conflict, "account_exists");
        await Problem(await admin.PutAsJsonAsync($"/api/v1/identity/users/{id}", new AccountUpdateRequest(null!, "bad")), HttpStatusCode.BadRequest, "invalid_account");
        await Problem(await admin.PutAsJsonAsync($"/api/v1/identity/users/{Guid.NewGuid()}", new AccountUpdateRequest("missing", "missing@example.test")), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Permission_assignment_validates_catalog_and_prevents_privilege_escalation()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var admin = factory.CreateClient(); var adminPair = await Login(admin);
        var role = await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/roles", new RoleRequest("授权员", ["system.permissions.manage"])));
        var id = await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/users", new AccountRequest("grantor", "grantor@example.test", "StrongPassword123", [role])));
        await Problem(await admin.PutAsJsonAsync($"/api/v1/identity/users/{id}/permissions", new AssignPermissionsRequest(["unknown"])), HttpStatusCode.BadRequest, "invalid_permission");
        await Problem(await admin.PutAsJsonAsync($"/api/v1/identity/users/{id}/permissions", new AssignPermissionsRequest(null!)), HttpStatusCode.BadRequest, "invalid_permission");
        await Problem(await admin.PutAsJsonAsync($"/api/v1/identity/users/{Guid.NewGuid()}/permissions", new AssignPermissionsRequest([])), HttpStatusCode.NotFound, "not_found");
        await Problem(await admin.PostAsJsonAsync("/api/v1/identity/roles", new RoleRequest(" Administrator ", [])), HttpStatusCode.Conflict, "builtin_role");
        using var grantor = factory.CreateClient(); await Login(grantor, "grantor", "StrongPassword123");
        await Data<AccountView[]>(await grantor.GetAsync("/api/v1/identity/users"));
        await Problem(await grantor.PutAsJsonAsync($"/api/v1/identity/users/{id}/permissions", new AssignPermissionsRequest(["system.storage.manage"])), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await grantor.PutAsJsonAsync($"/api/v1/identity/roles/{role}", new RoleRequest("授权员", ["system.storage.manage"])), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await grantor.PutAsJsonAsync($"/api/v1/identity/users/{adminPair.User.Id}/permissions", new AssignPermissionsRequest([])), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await grantor.PutAsJsonAsync($"/api/v1/identity/users/{id}", new AccountUpdateRequest("changed", "changed@example.test")), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task New_endpoints_require_authentication_and_administration_permission()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var anonymous = factory.CreateClient();
        var id = Guid.NewGuid();
        await Problem(await anonymous.GetAsync("/api/v1/identity/permissions"), HttpStatusCode.Unauthorized, "unauthorized");
        await Problem(await anonymous.PutAsJsonAsync($"/api/v1/identity/users/{id}", new AccountUpdateRequest("reader", "reader@example.test")), HttpStatusCode.Unauthorized, "unauthorized");
        await Problem(await anonymous.PutAsJsonAsync($"/api/v1/identity/users/{id}/permissions", new AssignPermissionsRequest([])), HttpStatusCode.Unauthorized, "unauthorized");
        using var admin = factory.CreateClient(); await Login(admin);
        id = await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/users", new AccountRequest("reader", "reader@example.test", "StrongPassword123", [])));
        await Login(anonymous, "reader", "StrongPassword123");
        await Problem(await anonymous.GetAsync("/api/v1/identity/permissions"), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await anonymous.PutAsJsonAsync($"/api/v1/identity/users/{id}/permissions", new AssignPermissionsRequest(["study.search"])), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await anonymous.PutAsJsonAsync($"/api/v1/identity/users/{id}", new AccountUpdateRequest("reader", "other@example.test")), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Catalog_navigation_and_openapi_describe_new_modules_and_contracts()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var admin = factory.CreateClient(); await Login(admin);
        var catalog = await Data<ModulePermission[]>(await admin.GetAsync("/api/v1/identity/permissions"));
        Assert.Contains(catalog, x => x.Key == "study.search" && x.RouteKey == "study-projects");
        var menus = await Data<JsonElement>(await admin.GetAsync("/api/v1/web/navigation/menus"));
        var entries = menus.EnumerateArray().ToDictionary(x => x.GetProperty("id").GetString()!);
        Assert.Equal("系统监控", entries["system.monitor"].GetProperty("title").GetString());
        Assert.Equal("system.management", entries["system.files"].GetProperty("parentId").GetString());
        Assert.Equal("knowledge", entries["knowledge.projects"].GetProperty("parentId").GetString());
        Assert.Equal("题库管理", entries["knowledge.practice.questions"].GetProperty("title").GetString());
        Assert.Contains("system-users", menus.ToString()); Assert.Contains("study-progress", menus.ToString());
        var document = await admin.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/v1/identity/users/{id}/permissions", out _));
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/v1/identity/permissions", out _));
    }
}
