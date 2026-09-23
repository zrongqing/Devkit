using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Devkit.Server.Application.Contracts.Auth;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.Workspace;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace Devkit.Server.Api.IntegrationTests;

internal static class WorkspaceTestSupport
{
    public static async Task<T> Data<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {status}, received {response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
        return json.RootElement.GetProperty("data").Deserialize<T>(JsonData.Options)!;
    }
    public static async Task Problem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, json.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("stackTrace", json.ToString(), StringComparison.OrdinalIgnoreCase);
    }
    public static async Task<TokenPairDto> Login(HttpClient client, string account = "admin", string password = "admin")
    {
        var pair = await Data<TokenPairDto>(await client.PostAsJsonAsync("/api/v1/auth/login", new { account, password }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pair.AccessToken);
        return pair;
    }
    public static async Task<KnowledgeBase> Base(HttpClient client, string name = "考勤制度") => await Data<KnowledgeBase>(await client.PostAsJsonAsync("/api/v1/exam-study/knowledge-bases", new NamedRequest(name)));
    public static async Task<StudyProject> Project(HttpClient client, params Guid[] bases) => await Data<StudyProject>(await client.PostAsJsonAsync("/api/v1/exam-study/projects", new ProjectRequest("入职考试", "", null, bases)));
    public static async Task<SourceDetailView> Source(DevkitApiFactory factory, HttpClient client, Guid kb, string text = "第一条 请假应提前一天提交申请。", Guid? fileId = null)
    {
        var job = await Data<WorkJob>(await client.PostAsJsonAsync($"/api/v1/exam-study/knowledge-bases/{kb}/sources", new SourceRequest("请假制度", fileId, text)), HttpStatusCode.Accepted);
        await factory.RunJobAsync(job.Id);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Devkit.Server.Infrastructure.Persistence.DevkitDbContext>();
        var version = await db.Set<SourceVersion>().FindAsync(job.TargetId);
        return await Data<SourceDetailView>(await client.GetAsync($"/api/v1/exam-study/sources/{version!.SourceId}"));
    }
    public static QuestionRequest Question(Guid kb, string type = "single", Citation[]? citations = null) => new(kb, type, "请假需要提前多久申请？", [new("A", "一天"), new("B", "两天"), new("C", "三天")], type == "multiple" ? ["A", "B"] : ["A"], "见请假制度。", citations ?? []);
    public static async Task<Question> ApprovedQuestion(HttpClient client, Guid kb, Citation[]? citations = null)
    {
        var q = await Data<Question>(await client.PostAsJsonAsync("/api/v1/exam-study/questions", Question(kb, citations: citations)));
        return await Data<Question>(await client.PutAsJsonAsync($"/api/v1/exam-study/questions/{q.Id}/status", new { status = "approved" }));
    }
    public static Citation Citation(SourceDetailView source)
    {
        var c = source.Chunks.First(); var v = source.Versions.Single(x => x.Id == c.VersionId);
        return new(c.Id, c.SourceId, c.VersionId, c.KnowledgeBaseId, v.FileId, v.Title, c.Location, c.Page, c.Text);
    }
    public static async Task<Guid> StudyUser(HttpClient admin, string name, params string[] extraPermissions)
    {
        var role = await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/roles", new RoleRequest($"role-{name}", ["exam-study.access", .. extraPermissions])));
        return await Data<Guid>(await admin.PostAsJsonAsync("/api/v1/identity/users", new AccountRequest(name, $"{name}@example.test", "StrongPassword123", [role])));
    }
}
