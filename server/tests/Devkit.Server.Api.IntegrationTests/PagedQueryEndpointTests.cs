using System.Net;
using System.Net.Http.Json;
using System.Linq.Expressions;
using System.Text.Json;
using Devkit.Application.Application.Administration;
using Devkit.Application.Application.Navigation;
using Devkit.Application.Domain.Organization;
using Devkit.Application.Infrastructure.Persistence;
using Devkit.Business.Application.Workspace;
using Devkit.Business.Domain.Workspace;
using Devkit.Business.Infrastructure.Persistence;
using Devkit.DomainCommons.Querying;
using Devkit.FileStorage.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class PagedQueryEndpointTests
{
    [Fact]
    public async Task Every_mapped_table_has_a_scoped_default_query_including_composite_keys()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        using var identityScope = factory.IdentityServices.CreateScope();
        using var businessScope = factory.Services.CreateScope();
        using var fileScope = factory.FileServices.CreateScope();
        Devkit.Infrastructure.Persistence.PagedDbContext[] contexts = [identityScope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            businessScope.ServiceProvider.GetRequiredService<BusinessDbContext>(), fileScope.ServiceProvider.GetRequiredService<FileStorageDbContext>()];
        var method = typeof(IPagedQueryStore).GetMethod(nameof(IPagedQueryStore.PageAsync))!;
        foreach (var db in contexts)
        foreach (var entity in db.Model.GetEntityTypes().Where(t => t.FindPrimaryKey() is not null && t.BaseType is null))
        {
            var row = Expression.Parameter(entity.ClrType, "row");
            var predicate = Expression.Lambda(typeof(Func<,>).MakeGenericType(entity.ClrType, typeof(bool)), Expression.Constant(false), row);
            var task = (Task)method.MakeGenericMethod(entity.ClrType).Invoke(db, [new PageQuery(), predicate, CancellationToken.None])!;
            await task;
            var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            Assert.Equal(0, result.GetType().GetProperty("Total")!.GetValue(result));
        }
    }
    private static readonly string[] QueryPaths = [
        "/api/v1/exam-study/knowledge-bases/query", "/api/v1/exam-study/projects/query", "/api/v1/exam-study/questions/query", "/api/v1/exam-study/jobs/query",
        "/api/v1/files/query", "/api/v1/storage/jobs/query", "/api/v1/identity/users/query", "/api/v1/identity/roles/query",
        "/api/v1/identity/users/disabled/query", "/api/v1/identity/audit/query", "/api/v1/web/menu-management/menus/query",
    ];

    [Fact]
    public async Task Query_endpoints_authenticate_authorize_and_return_typed_pages()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient();
        foreach (var path in QueryPaths) await Problem(await client.PostAsJsonAsync(path, new PageQuery()), HttpStatusCode.Unauthorized, "unauthorized");
        await Login(client);
        foreach (var path in QueryPaths)
        {
            var response = await client.PostAsJsonAsync(path, new PageQuery());
            Assert.True(response.IsSuccessStatusCode, path + ": " + await response.Content.ReadAsStringAsync());
            var page = await Data<PageResult<JsonElement>>(response);
            Assert.Equal(1, page.Page); Assert.Equal(20, page.PageSize);
            var pageCount = path.EndsWith("/menus/query") ? page.Items.Count(row => row.GetProperty("parentCode").ValueKind == JsonValueKind.Null) : page.Items.Count;
            Assert.True(page.Total >= pageCount, $"{path}: total must count all matching rows (tree roots for menus)");
        }
        await Data<Guid>(await client.PostAsJsonAsync("/api/v1/identity/users", new AccountRequest("bare-query-user", "bare-query@example.test", "StrongPassword123", [])));
        await Login(client, "bare-query-user", "StrongPassword123");
        foreach (var path in QueryPaths) await Problem(await client.PostAsJsonAsync(path, new PageQuery()), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Database_paging_combines_filters_sorts_and_stable_keys_and_preserves_scope()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var admin = factory.CreateClient(); await Login(admin);
        await Base(admin, "outside");
        await StudyUser(admin, "page-owner"); await StudyUser(admin, "page-other");
        using var owner = factory.CreateClient(); await Login(owner, "page-owner", "StrongPassword123");
        foreach (var (name, description) in new[] { ("match-b", "keep"), ("match-a", "keep"), ("match-c", "drop"), ("unrelated", "keep"), ("match-a", "keep") })
            await Data<KnowledgeBase>(await owner.PostAsJsonAsync("/api/v1/exam-study/knowledge-bases", new NamedRequest(name, description)));
        var query = new PageQuery(1, 1, [new("name", "match"), new("description", "keep")], [new("name"), new("description", "desc")]);
        var first = await Data<PageResult<KnowledgeBase>>(await owner.PostAsJsonAsync("/api/v1/exam-study/knowledge-bases/query", query));
        var second = await Data<PageResult<KnowledgeBase>>(await owner.PostAsJsonAsync("/api/v1/exam-study/knowledge-bases/query", query with { Page = 2 }));
        Assert.Equal(3, first.Total); Assert.Equal("match-a", Assert.Single(first.Items).Name);
        Assert.Equal("match-a", Assert.Single(second.Items).Name); Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        var repeated = await Data<PageResult<KnowledgeBase>>(await owner.PostAsJsonAsync("/api/v1/exam-study/knowledge-bases/query", query));
        Assert.Equal(first.Items[0].Id, repeated.Items[0].Id);
        var beyond = await Data<PageResult<KnowledgeBase>>(await owner.PostAsJsonAsync("/api/v1/exam-study/knowledge-bases/query", query with { Page = 20 }));
        Assert.Empty(beyond.Items); Assert.Equal(3, beyond.Total);
        using var other = factory.CreateClient(); await Login(other, "page-other", "StrongPassword123");
        Assert.Empty((await Data<PageResult<KnowledgeBase>>(await other.PostAsJsonAsync("/api/v1/exam-study/knowledge-bases/query?all=true", new PageQuery()))).Items);
    }

    [Fact]
    public async Task Invalid_queries_fail_with_ProblemDetails_and_do_not_expose_identity_secrets()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        var path = "/api/v1/exam-study/projects/query";
        PageQuery[] invalid = [new(Page: 0), new(PageSize: 101), new(Page: int.MaxValue), new(Filters: [new("missing", "x")]),
            new(Filters: [new("id", "not-a-guid", "eq")]), new(Sorts: [new("name", "sideways")]), new(Sorts: [new("name"), new("Name")]),
            new(Filters: [new("name", "x", "raw-sql")]), new(Filters: [null!]), new(Sorts: [null!])];
        foreach (var query in invalid) await Problem(await client.PostAsJsonAsync(path, query), HttpStatusCode.BadRequest, "invalid_query");
        foreach (var secret in new[] { "passwordHash", "userClaims", "securityStamp", "userTokens" })
            await Problem(await client.PostAsJsonAsync("/api/v1/identity/users/query", new PageQuery(Filters: [new(secret, "x")])), HttpStatusCode.BadRequest, "invalid_query");
        var users = await Data<PageResult<JsonElement>>(await client.PostAsJsonAsync("/api/v1/identity/users/query", new PageQuery()));
        Assert.All(users.Items, row => Assert.False(row.TryGetProperty("passwordHash", out _)));
    }

    [Fact]
    public async Task Sources_questions_progress_and_file_projection_queries_obey_context()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client); await factory.InitializeStorageAsync();
        var kb = await Base(client); var other = await Base(client, "other");
        await Source(factory, client, kb.Id); await ApprovedQuestion(client, kb.Id);
        var project = await Project(client, kb.Id);
        Assert.Single((await Data<PageResult<KnowledgeSource>>(await client.PostAsJsonAsync($"/api/v1/exam-study/knowledge-bases/{kb.Id}/sources/query", new PageQuery(Filters: [new("status", "可检索")])))).Items);
        Assert.Empty((await Data<PageResult<KnowledgeSource>>(await client.PostAsJsonAsync($"/api/v1/exam-study/knowledge-bases/{other.Id}/sources/query", new PageQuery()))).Items);
        Assert.Single((await Data<PageResult<Question>>(await client.PostAsJsonAsync($"/api/v1/exam-study/questions/query?knowledgeBaseId={kb.Id}", new PageQuery(Filters: [new("type", "单选"), new("status", "已审核")])))).Items);
        await Data<PageResult<Mistake>>(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/mistakes/query", new PageQuery()));
        await Data<PageResult<AttemptSummary>>(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/attempts/query", new PageQuery()));
        using var content = new MultipartFormDataContent(); content.Add(new ByteArrayContent("file content"u8.ToArray()), "file", "query.txt");
        var file = await Data<Devkit.FileStorage.Application.Files.FileView>(await client.PostAsync("/api/v1/files/?purpose=general", content));
        using (var scope = factory.FileServices.CreateScope())
            await scope.ServiceProvider.GetRequiredService<Devkit.FileStorage.Application.Files.IFileService>().QueryFilesAsync(
                new Devkit.FileStorage.Application.Files.Actor(Guid.NewGuid(), true, []), new PageQuery(), default);
        var files = await Data<PageResult<Devkit.FileStorage.Application.Files.FileView>>(await client.PostAsJsonAsync("/api/v1/files/query", new PageQuery(Filters: [new("name", "query"), new("referenceCount", "0", "eq")], Sorts: [new("length", "desc"), new("name")])));
        Assert.Equal(file.Id, Assert.Single(files.Items).Id);
        var displayFiltered = await Data<PageResult<Devkit.FileStorage.Application.Files.FileView>>(await client.PostAsJsonAsync("/api/v1/files/query", new PageQuery(Filters: [new("status", "可检索"), new("length", "12")])));
        Assert.Equal(file.Id, Assert.Single(displayFiltered.Items).Id);
        await Data<PageResult<JsonElement>>(await client.PostAsJsonAsync($"/api/v1/files/{file.Id}/references/query", new PageQuery()));
        await Problem(await client.PostAsJsonAsync($"/api/v1/files/{Guid.NewGuid()}/references/query", new PageQuery()), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Audit_and_jobs_query_the_full_dataset_instead_of_old_list_limits()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        using (var scope = factory.IdentityServices.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AddRange(Enumerable.Range(1, 75).Select(i => new AdministrationEvent { Operation = $"seed-query-{i:D3}", OccurredUtc = DateTime.UtcNow, TraceId = "isolated" })); await db.SaveChangesAsync();
        }
        var audit = await Data<PageResult<AdministrationEvent>>(await client.PostAsJsonAsync("/api/v1/identity/audit/query", new PageQuery(4, 20, [new("operation", "seed-query-")], [new("id")])));
        Assert.Equal(75, audit.Total); Assert.Equal(15, audit.Items.Count);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
            db.AddRange(Enumerable.Range(1, 205).Select(i => new WorkJob { Kind = "ingest", Error = $"seed-query-{i:D3}" })); await db.SaveChangesAsync();
        }
        var jobs = await Data<PageResult<WorkJob>>(await client.PostAsJsonAsync("/api/v1/exam-study/jobs/query", new PageQuery(11, 20, [new("error", "seed-query-")], [new("error")])));
        Assert.Equal(205, jobs.Total); Assert.Equal(5, jobs.Items.Count);
    }

    [Fact]
    public async Task Organization_and_menu_queries_support_derived_fields_and_keep_tree_ancestors()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); var actor = await Login(client);
        var department = await Data<Guid>(await client.PostAsJsonAsync("/api/v1/identity/organization/departments", new { name = "query department" }));
        var position = await Data<Guid>(await client.PostAsJsonAsync("/api/v1/identity/organization/positions", new { name = "engineer", departmentId = department }));
        await client.PutAsync($"/api/v1/identity/organization/positions/{position}/members/{actor.User.Id}", null);
        var positions = await Data<PageResult<Position>>(await client.PostAsJsonAsync($"/api/v1/identity/organization/departments/{department}/positions/query", new PageQuery(Filters: [new("members", "admin")])));
        Assert.Equal(position, Assert.Single(positions.Items).Id);
        var menuPage = await Data<PageResult<MenuView>>(await client.PostAsJsonAsync("/api/v1/web/menu-management/menus/query", new PageQuery(Filters: [new("menuCode", "system.menus")])));
        Assert.Contains(menuPage.Items, x => x.MenuCode == "system.menus");
        foreach (var child in menuPage.Items.Where(x => x.ParentCode is not null)) Assert.Contains(menuPage.Items, p => p.MenuCode == child.ParentCode);
        var compare = await Data<PageResult<MenuDifference>>(await client.PostAsJsonAsync("/api/v1/web/menu-management/compare/query", new MenuCompareQuery([], new PageQuery(Sorts: [new("menuCode")]))));
        Assert.NotEmpty(compare.Items);
        var fields = await Data<PageResult<MenuFieldRow>>(await client.PostAsJsonAsync("/api/v1/web/menu-management/fields/query", new MenuFieldsQuery([], "system.menus", new PageQuery(Filters: [new("name", "名称")]))));
        Assert.Equal("title", Assert.Single(fields.Items).Key);
    }
}
