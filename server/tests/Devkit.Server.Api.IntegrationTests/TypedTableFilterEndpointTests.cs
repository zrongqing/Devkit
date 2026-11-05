using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Devkit.Application.Application.Administration;
using Devkit.Application.Application.Navigation;
using Devkit.Business.Domain.Workspace;
using Devkit.DomainCommons.Querying;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class TypedTableFilterEndpointTests
{
    private const string Users = "/api/v1/identity/users/query";
    private const string Menus = "/api/v1/web/menu-management/menus/query";

    [Fact]
    public async Task Role_filter_uses_stable_ids_any_match_and_combines_with_other_columns_before_paging()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        var first = await Data<Guid>(await client.PostAsJsonAsync("/api/v1/identity/roles", new RoleRequest("typed-role", [])));
        var second = await Data<Guid>(await client.PostAsJsonAsync("/api/v1/identity/roles", new RoleRequest("typed-role-extra", [])));
        foreach (var (name, roles) in new (string, Guid[])[] { ("typed-a", [first]), ("typed-b", [second]), ("typed-c", [first, second]), ("typed-none", []) })
            await Data<Guid>(await client.PostAsJsonAsync("/api/v1/identity/users", new AccountRequest(name, name + "@example.test", "StrongPassword123", roles)));
        var query = new PageQuery(1, 1, [new("roleIds", Operator: "anyOf", Values: [first.ToString(), second.ToString()]), new("userName", "typed-")], [new("userName")]);
        var one = await Data<PageResult<AccountView>>(await client.PostAsJsonAsync(Users, query));
        var two = await Data<PageResult<AccountView>>(await client.PostAsJsonAsync(Users, query with { Page = 2 }));
        Assert.Equal(3, one.Total); Assert.Equal("typed-a", Assert.Single(one.Items).UserName);
        Assert.Equal("typed-b", Assert.Single(two.Items).UserName);
        var onlyFirst = await Data<PageResult<AccountView>>(await client.PostAsJsonAsync(Users, query with { PageSize = 100, Filters = [new("roleIds", Operator: "anyOf", Values: [first.ToString()])] }));
        Assert.Equal(new[] { "typed-a", "typed-c" }, onlyFirst.Items.Select(x => x.UserName));
        Assert.All(onlyFirst.Items, x => Assert.Contains(first, x.RoleIds));
        (await client.PutAsJsonAsync($"/api/v1/identity/roles/{first}", new RoleRequest("角色已改名", []))).EnsureSuccessStatusCode();
        var renamed = await Data<PageResult<AccountView>>(await client.PostAsJsonAsync(Users, query with { PageSize = 100 }));
        Assert.Equal(3, renamed.Total);
        Assert.Contains("角色已改名", renamed.Items.Single(x => x.UserName == "typed-a").Roles);
        var options = await Data<RoleView[]>(await client.GetAsync("/api/v1/identity/roles"));
        Assert.Contains(options, x => x.Id == first && x.Name == "角色已改名");
        Assert.Contains(options, x => x.Name == "Administrator");
        // A deleted/stale candidate cannot accidentally match another similarly named role.
        Assert.Empty((await Data<PageResult<AccountView>>(await client.PostAsJsonAsync(Users, new PageQuery(Filters: [new("roleIds", Operator: "anyOf", Values: [Guid.NewGuid().ToString()])])))).Items);
    }

    [Fact]
    public async Task Menu_options_use_canonical_values_keep_ancestors_and_accept_legacy_display_text()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        async Task<PageResult<MenuView>> Query(params FieldFilter[] filters) => await Data<PageResult<MenuView>>(await client.PostAsJsonAsync(Menus, new PageQuery(PageSize: 100, Filters: filters)));
        var modules = await Query(new FieldFilter("type", Operator: "in", Values: ["module"]));
        var legacyModules = await Query(new FieldFilter("type", "模块"));
        Assert.Equal(modules.Items.Select(x => x.MenuCode), legacyModules.Items.Select(x => x.MenuCode));
        Assert.Contains(modules.Items, x => x.Type == "module");
        foreach (var child in modules.Items.Where(x => x.ParentCode is not null)) Assert.Contains(modules.Items, x => x.MenuCode == child.ParentCode);
        var directories = await Query(new FieldFilter("type", Operator: "in", Values: ["directory"]));
        Assert.NotEmpty(directories.Items); Assert.All(directories.Items, x => Assert.Equal("directory", x.Type));
        var legacyDirectories = await Query(new FieldFilter("type", "目录"));
        Assert.Equal(directories.Items.Select(x => x.MenuCode), legacyDirectories.Items.Select(x => x.MenuCode));
        var all = await Query();
        var both = await Query(new FieldFilter("type", Operator: "in", Values: ["module", "directory"]));
        Assert.Equal(all.Items.Select(x => x.MenuCode), both.Items.Select(x => x.MenuCode));
        var combined = await Query(new FieldFilter("TYPE", Operator: "in", Values: ["module"]), new FieldFilter("enabled", Operator: "in", Values: ["true"]), new FieldFilter("menuCode", "system.menus", "eq"));
        Assert.Contains(combined.Items, x => x.MenuCode == "system.menus" && x.Enabled);
        Assert.Empty((await Query(new FieldFilter("type", "目录"), new FieldFilter("menuCode", "system.menus", "eq"))).Items);
        Assert.Empty((await Query(new FieldFilter("type", "没有这种类型"))).Items);
    }

    [Fact]
    public async Task Typed_database_filters_combine_enum_choices_ranges_and_authorized_scope()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        var kb = await Base(client); await ApprovedQuestion(client, kb.Id);
        var questions = await Data<PageResult<Question>>(await client.PostAsJsonAsync($"/api/v1/exam-study/questions/query?knowledgeBaseId={kb.Id}",
            new PageQuery(Filters: [new("type", Operator: "in", Values: ["single", "multiple"]), new("status", Operator: "in", Values: ["approved"]),
                new("createdAtUtc", DateTimeOffset.UtcNow.AddHours(-1).ToOffset(TimeSpan.FromHours(8)).ToString("O"), "gte"),
                new("createdAtUtc", DateTimeOffset.UtcNow.AddHours(1).ToOffset(TimeSpan.FromHours(-5)).ToString("O"), "lte"), new("revision", "0", "gte")])));
        Assert.Single(questions.Items);
        Assert.Empty((await Data<PageResult<Question>>(await client.PostAsJsonAsync($"/api/v1/exam-study/questions/query?knowledgeBaseId={kb.Id}",
            new PageQuery(Filters: [new("status", Operator: "in", Values: ["draft", "disabled"])])))).Items);
        await StudyUser(client, "typed-owner");
        using var owner = factory.CreateClient(); await Login(owner, "typed-owner", "StrongPassword123");
        Assert.Empty((await Data<PageResult<Question>>(await owner.PostAsJsonAsync("/api/v1/exam-study/questions/query", new PageQuery(Filters: [new("status", Operator: "in", Values: ["approved"])])))).Items);
    }

    [Fact]
    public async Task Invalid_multivalue_filters_return_problem_details_even_without_matching_rows()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        FieldFilter[] invalid = [new("roleIds", Operator: "anyOf"), new("roleIds", Operator: "anyOf", Values: []),
            new("roleIds", Operator: "anyOf", Values: [null!]), new("roleIds", Operator: "anyOf", Values: ["not-a-guid"]),
            new("roleIds", Operator: "anyOf", Values: Enumerable.Repeat(Guid.NewGuid().ToString(), 101).ToArray()),
            new("roleIds", "x", "anyOf", [Guid.NewGuid().ToString()]), new("roleIds", Operator: "in", Values: [Guid.NewGuid().ToString()]),
            new("userName", Operator: "anyOf", Values: ["admin"]), new("userName", "admin", "eq", ["admin"]),
            new("missing", Operator: "in", Values: ["x"]), new("roleIds", Operator: "anyOf", Values: [new string('x', 513)])];
        foreach (var filter in invalid) await Problem(await client.PostAsJsonAsync(Users, new PageQuery(Filters: [new("userName", "no-matches"), filter])), HttpStatusCode.BadRequest, "invalid_query");
        foreach (var filter in new FieldFilter[] { new("type", Operator: "in", Values: ["模块"]), new("type", "module", "gte"), new("enabled", Operator: "in", Values: ["maybe"]) })
            await Problem(await client.PostAsJsonAsync(Menus, new PageQuery(Filters: [filter])), HttpStatusCode.BadRequest, "invalid_query");
        await Problem(await client.PostAsJsonAsync("/api/v1/exam-study/questions/query", new PageQuery(Filters: [new("status", Operator: "in", Values: ["unknown"])])), HttpStatusCode.BadRequest, "invalid_query");
    }

    [Fact]
    public async Task Role_candidates_and_typed_queries_obey_authentication_and_management_permissions()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient();
        var query = new PageQuery(Filters: [new("roleIds", Operator: "anyOf", Values: [Guid.NewGuid().ToString()])]);
        await Problem(await client.GetAsync("/api/v1/identity/roles"), HttpStatusCode.Unauthorized, "unauthorized");
        await Problem(await client.PostAsJsonAsync(Users, query), HttpStatusCode.Unauthorized, "unauthorized");
        await Login(client);
        var managerRole = await Data<Guid>(await client.PostAsJsonAsync("/api/v1/identity/roles", new RoleRequest("typed-user-manager", ["system.users.manage"])));
        await Data<Guid>(await client.PostAsJsonAsync("/api/v1/identity/users", new AccountRequest("typed-manager", "typed-manager@example.test", "StrongPassword123", [managerRole])));
        await Data<Guid>(await client.PostAsJsonAsync("/api/v1/identity/users", new AccountRequest("typed-bare", "typed-bare@example.test", "StrongPassword123", [])));
        await Login(client, "typed-manager", "StrongPassword123");
        Assert.NotEmpty(await Data<RoleView[]>(await client.GetAsync("/api/v1/identity/roles")));
        var managed = await Data<PageResult<AccountView>>(await client.PostAsJsonAsync(Users, query with { Filters = [new("roleIds", Operator: "anyOf", Values: [managerRole.ToString()])] }));
        Assert.Equal("typed-manager", Assert.Single(managed.Items).UserName);
        // Existing read policy allows user managers to read menus for grant dialogs.
        Assert.NotEmpty((await Data<PageResult<MenuView>>(await client.PostAsJsonAsync(Menus, new PageQuery(Filters: [new("type", Operator: "in", Values: ["module"])])))).Items);
        await Login(client, "typed-bare", "StrongPassword123");
        await Problem(await client.GetAsync("/api/v1/identity/roles"), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await client.PostAsJsonAsync(Users, query), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await client.PostAsJsonAsync(Menus, new PageQuery(Filters: [new("type", Operator: "in", Values: ["module"])])), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task OpenApi_describes_additive_multivalue_and_stable_role_id_contracts()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateIdentityClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var schemas = doc.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("FieldFilter").GetProperty("properties").TryGetProperty("values", out _));
        Assert.True(schemas.GetProperty("AccountView").GetProperty("properties").TryGetProperty("roleIds", out _));
    }

    private sealed record SampleRow(Guid Id, Guid[] RoleIds, bool Enabled, int Count, string? Name);

    [Fact]
    public void Shared_snapshot_and_expression_filters_have_equal_typed_semantics()
    {
        var role = Guid.NewGuid();
        SampleRow[] rows = [new(Guid.NewGuid(), [role], false, 0, null), new(Guid.NewGuid(), [], true, 1, "one"), new(Guid.NewGuid(), [role], true, 2, "two")];
        var query = new PageQuery(Filters: [new("roleIds", Operator: "anyOf", Values: [role.ToString()]), new("enabled", Operator: "in", Values: ["false", "true"]), new("count", "0", "gte"), new("count", "1", "lte")]);
        Assert.Equal(rows[0].Id, Assert.Single(PageQueries.SelectSnapshot(rows, query)).Id);
        Assert.Equal(rows[0].Id, Assert.Single(PageQueries.Filter(rows.AsQueryable(), query)).Id);
        var empty = query with { Filters = [new("roleIds", Operator: "anyOf", Values: ["invalid"])] };
        Assert.Throws<QueryValidationException>(() => PageQueries.SelectSnapshot(Array.Empty<SampleRow>(), empty));
        Assert.Throws<QueryValidationException>(() => PageQueries.Filter(Array.Empty<SampleRow>().AsQueryable(), empty));
    }
}
