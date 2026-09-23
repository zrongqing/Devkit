using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.Workspace;
using Devkit.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class StudyEndpointTests
{
    [Fact]
    public async Task Source_versions_are_searchable_only_inside_the_linked_project_scope()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        var kb = await Base(client); var other = await Base(client, "其他制度");
        var source = await Source(factory, client, kb.Id);
        await Source(factory, client, other.Id, "不在项目范围的制度");
        var project = await Project(client, kb.Id);
        var first = await Data<SearchResponse>(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/search", new SearchRequest("请假")));
        Assert.Single(first.Items); Assert.Equal(kb.Id, first.Items[0].Citation.KnowledgeBaseId);
        Assert.Contains("提前一天", first.Items[0].Citation.Text);

        var job = await Data<WorkJob>(await client.PutAsJsonAsync($"/api/v1/exam-study/knowledge-bases/{kb.Id}/sources/{source.Source.Id}", new SourceRequest("更新的制度", null, "第一条 请假应提前两天提交申请。", source.Source.Revision)), HttpStatusCode.Accepted);
        await factory.RunJobAsync(job.Id);
        var updated = await Data<SourceDetailView>(await client.GetAsync($"/api/v1/exam-study/sources/{source.Source.Id}"));
        Assert.Equal(2, updated.Versions.Count); Assert.Equal(2, updated.Source.PublishedRevision);
        Assert.Contains(updated.Versions, v => v.Text.Contains("提前一天"));
        var found = await Data<SearchResponse>(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/search", new SearchRequest("请假")));
        Assert.Single(found.Items); Assert.Contains("提前两天", found.Items[0].Citation.Text);
        Assert.DoesNotContain(source.Chunks[0].VersionId.ToString(), factory.External.LastQuery.ToString());
        var outside = await Data<SearchResponse>(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/search", new SearchRequest("制度", KnowledgeBaseId: other.Id)));
        Assert.Empty(outside.Items);
    }

    [Fact]
    public async Task Search_rechecks_associations_after_external_retrieval()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        var kb = await Base(client); await Source(factory, client, kb.Id); var project = await Project(client, kb.Id);
        factory.External.AfterQuery = async () =>
        {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DevkitDbContext>();
            await db.Set<StudyProject>().Where(x => x.Id == project.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.KnowledgeBaseIdsJson, "[]"));
        };
        var result = await Data<SearchResponse>(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/search", new SearchRequest("请假")));
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Source_changes_stale_reviewed_questions_and_reject_old_citations()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        var kb = await Base(client); var source = await Source(factory, client, kb.Id);
        var q = await ApprovedQuestion(client, kb.Id, [Citation(source)]);
        await Data<WorkJob>(await client.PutAsJsonAsync($"/api/v1/exam-study/knowledge-bases/{kb.Id}/sources/{source.Source.Id}", new SourceRequest("修改制度", null, "提前两天。", source.Source.Revision)), HttpStatusCode.Accepted);
        var questions = await Data<Question[]>(await client.GetAsync("/api/v1/exam-study/questions"));
        Assert.Equal("stale", Assert.Single(questions).Status);
        await Problem(await client.PutAsJsonAsync($"/api/v1/exam-study/questions/{q.Id}/status", new { status = "approved" }), HttpStatusCode.BadRequest, "invalid_request");
        var project = await Project(client, kb.Id);
        await Problem(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/attempts", new AttemptRequest(Count: 1)), HttpStatusCode.Conflict, "insufficient_questions");
    }

    [Fact]
    public async Task Exam_hides_answers_grades_snapshot_and_records_mistake_once()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        var kb = await Base(client); var q = await ApprovedQuestion(client, kb.Id); var project = await Project(client, kb.Id);
        var attempt = await Data<AttemptView>(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/attempts", new AttemptRequest(Count: 1)));
        Assert.Null(attempt.Questions[0].Answers); Assert.Null(attempt.Questions[0].Explanation); Assert.Null(attempt.Score);
        await Data<Question>(await client.PutAsJsonAsync($"/api/v1/exam-study/questions/{q.Id}", Question(kb.Id) with { Answers = ["B"], Revision = q.Revision }));
        attempt = await Data<AttemptView>(await client.PutAsJsonAsync($"/api/v1/exam-study/attempts/{attempt.Id}/answers/{q.Id}", new AnswerRequest(["B"], attempt.Revision)));
        Assert.Null(attempt.Questions[0].Answers);
        var finished = await Data<AttemptView>(await client.PostAsync($"/api/v1/exam-study/attempts/{attempt.Id}/submit", null));
        Assert.Equal("submitted", finished.Status); Assert.Equal(0m, finished.Score);
        Assert.NotNull(finished.Questions[0].Answers);
        Assert.Equal(new[] { "A" }, finished.Questions[0].Answers);
        await Data<AttemptView>(await client.PostAsync($"/api/v1/exam-study/attempts/{attempt.Id}/submit", null));
        var progress = await Data<ProgressView>(await client.GetAsync($"/api/v1/exam-study/projects/{project.Id}/progress"));
        Assert.Equal(1, progress.Answered); Assert.Equal(1, Assert.Single(progress.Mistakes).WrongCount);
    }

    [Fact]
    public async Task Expired_exam_rejects_late_answers_and_submits()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        var kb = await Base(client); var q = await ApprovedQuestion(client, kb.Id); var project = await Project(client, kb.Id);
        var a = await Data<AttemptView>(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/attempts", new AttemptRequest(Count: 1)));
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DevkitDbContext>().Set<StudyAttempt>().Where(x => x.Id == a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.DeadlineUtc, DateTime.UtcNow.AddMinutes(-1)));
        var result = await Data<AttemptView>(await client.PutAsJsonAsync($"/api/v1/exam-study/attempts/{a.Id}/answers/{q.Id}", new AnswerRequest(["A"], a.Revision)));
        Assert.Equal("submitted", result.Status); Assert.Equal(0m, result.Score); Assert.Empty(result.Questions[0].Selected);
    }

    [Fact]
    public async Task Practice_reveals_answer_and_mistake_review_tracks_mastery()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client);
        var kb = await Base(client); var q = await ApprovedQuestion(client, kb.Id); var project = await Project(client, kb.Id);
        var a = await Data<AttemptView>(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/attempts", new AttemptRequest("practice", 1)));
        a = await Data<AttemptView>(await client.PutAsJsonAsync($"/api/v1/exam-study/attempts/{a.Id}/answers/{q.Id}", new AnswerRequest(["C"], a.Revision)));
        Assert.False(a.Questions[0].Correct); Assert.NotNull(a.Questions[0].Answers); Assert.Equal(new[] { "A" }, a.Questions[0].Answers);
        var progress = await Data<ProgressView>(await client.GetAsync($"/api/v1/exam-study/projects/{project.Id}/progress"));
        var mistake = Assert.Single(progress.Mistakes);
        await Data<SuccessView>(await client.PutAsJsonAsync($"/api/v1/exam-study/mistakes/{mistake.Id}", new { mastered = true }));
        await Problem(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/attempts", new AttemptRequest("practice", 1, Selection: "mistakes")), HttpStatusCode.Conflict, "insufficient_questions");
    }

    [Theory]
    [InlineData("single", "X")]
    [InlineData("multiple", "A")]
    [InlineData("unknown", "A")]
    public async Task Invalid_question_answers_or_types_return_problem(string type, string answer)
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client); var kb = await Base(client);
        await Problem(await client.PostAsJsonAsync("/api/v1/exam-study/questions", Question(kb.Id, type) with { Answers = [answer] }), HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    public async Task Revision_conflict_keeps_the_existing_content()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client); var kb = await Base(client);
        await Data<KnowledgeBase>(await client.PutAsJsonAsync($"/api/v1/exam-study/knowledge-bases/{kb.Id}", new NamedRequest("新版", Revision: kb.Revision)));
        await Problem(await client.PutAsJsonAsync($"/api/v1/exam-study/knowledge-bases/{kb.Id}", new NamedRequest("旧页提交", Revision: kb.Revision)), HttpStatusCode.Conflict, "revision_conflict");
        Assert.Equal("新版", Assert.Single(await Data<KnowledgeBase[]>(await client.GetAsync("/api/v1/exam-study/knowledge-bases"))).Name);
    }

    [Theory]
    [InlineData("source-text")]
    [InlineData("project-bases")]
    [InlineData("question-options")]
    [InlineData("question-answers")]
    [InlineData("question-citations")]
    public async Task Explicit_null_fields_return_validation_problem_instead_of_server_error(string field)
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client); var kb = await Base(client);
        HttpResponseMessage response;
        if (field == "source-text") response = await client.PostAsJsonAsync($"/api/v1/exam-study/knowledge-bases/{kb.Id}/sources", new SourceRequest("条目", Guid.NewGuid(), null!));
        else if (field == "project-bases") response = await client.PostAsJsonAsync("/api/v1/exam-study/projects", new ProjectRequest("考试", "", null, null!));
        else
        {
            var q = Question(kb.Id);
            q = field switch { "question-options" => q with { Options = null! }, "question-answers" => q with { Answers = null! }, _ => q with { Citations = null! } };
            response = await client.PostAsJsonAsync("/api/v1/exam-study/questions", q);
        }
        await Problem(response, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    public async Task Model_and_index_failures_preserve_keyword_mode_and_report_errors()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient(); await Login(client); var kb = await Base(client);
        await Source(factory, client, kb.Id); var project = await Project(client, kb.Id);
        await Problem(await client.PostAsJsonAsync("/api/v1/exam-study/generation-jobs", new GenerationRequest(kb.Id, null)), HttpStatusCode.ServiceUnavailable, "model_not_configured");
        await Problem(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/ask", new SearchRequest("请假")), HttpStatusCode.ServiceUnavailable, "model_not_configured");
        Assert.Single((await Data<SearchResponse>(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/search", new SearchRequest("请假")))).Items);
        factory.External.Unavailable = true;
        await Problem(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/search", new SearchRequest("请假")), HttpStatusCode.ServiceUnavailable, "index_unavailable");
    }

    [Fact]
    public async Task Ask_stores_citations_and_rejects_invented_citation_numbers()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); factory.Model.ChatConfigured = true;
        using var client = factory.CreateClient(); await Login(client); var kb = await Base(client);
        var source = await Source(factory, client, kb.Id); var project = await Project(client, kb.Id);
        var answer = await Data<QueryHistory>(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/ask", new SearchRequest("请假")));
        Assert.Equal(source.Source.Id, JsonData.Read<Citation[]>(answer.CitationsJson)[0].SourceId);
        factory.Model.Reply = "伪造的依据 [999]";
        await Problem(await client.PostAsJsonAsync($"/api/v1/exam-study/projects/{project.Id}/ask", new SearchRequest("请假")), HttpStatusCode.ServiceUnavailable, "invalid_answer_citation");
        Assert.Single(await Data<QueryHistory[]>(await client.GetAsync($"/api/v1/exam-study/projects/{project.Id}/history")));
    }

    [Fact]
    public async Task Generated_questions_are_drafts_and_replaying_job_does_not_duplicate()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); factory.Model.ChatConfigured = true;
        using var client = factory.CreateClient(); await Login(client); var kb = await Base(client); var source = await Source(factory, client, kb.Id);
        factory.Model.Reply = JsonData.Write(new { questions = new[] { new { stem = "应当提前几天？", options = new[] { new { id = "A", text = "一天" }, new { id = "B", text = "两天" } }, answers = new[] { "A" }, explanation = "根据第一条", chunkIds = new[] { source.Chunks[0].Id } } } });
        var job = await Data<WorkJob>(await client.PostAsJsonAsync("/api/v1/exam-study/generation-jobs", new GenerationRequest(kb.Id, null, 1)), HttpStatusCode.Accepted);
        await factory.RunJobAsync(job.Id); await factory.RunJobAsync(job.Id);
        var q = Assert.Single(await Data<Question[]>(await client.GetAsync("/api/v1/exam-study/questions")));
        Assert.Equal("draft", q.Status); Assert.Equal(job.Id, q.GenerationJobId);
        Assert.Single(JsonData.Read<Citation[]>(q.CitationsJson));
    }

    [Fact]
    public async Task OpenApi_exposes_request_response_and_async_job_contracts()
    {
        using var factory = new DevkitApiFactory(); using var client = factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json"); var paths = doc.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/v1/exam-study/projects/{id}/search").GetProperty("post").TryGetProperty("requestBody", out _));
        Assert.True(paths.GetProperty("/api/v1/exam-study/knowledge-bases/{id}/sources").GetProperty("post").GetProperty("responses").TryGetProperty("202", out _));
        var schemas = doc.GetProperty("components").GetProperty("schemas");
        foreach (var schema in new[] { "SearchRequest", "SearchResponse", "AttemptView", "SourceDetailView", "FileView", "LocationRequest", "AccountRequest" }) Assert.True(schemas.TryGetProperty(schema, out _), schema);
    }
}
