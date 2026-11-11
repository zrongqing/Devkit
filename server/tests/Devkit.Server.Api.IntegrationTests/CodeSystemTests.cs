using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Devkit.Business.Api.Contracts;
using Devkit.Business.Application.CodeSystem;
using Devkit.Business.Application.Workspace;
using Devkit.Business.Domain.CodeSystem;
using Devkit.Business.Infrastructure.CodeSystem;
using Devkit.Business.Infrastructure.Persistence;
using Devkit.DomainCommons.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class CodeSystemTests
{
    private const string Root = "/api/v1/code-system";
    internal static readonly CodeDefinition Daily = new(1, "+08:00", "day", 1, 1, [
        new("literal","RK-"),new("year"),new("month"),new("day"),new("literal","-"),new("sequence",Width:4),new("literal","-A")]);
    internal static CodeDefinition Simple(int width, long start = 1) => new(1, "+08:00", "none", start, 1, [new("sequence", Width: width)]);
    internal static Dictionary<string, string?> Scenarios() => new()
    {
        ["CodeSystem:Scenarios:0:RuleKey"] = "wms.receipt",
        ["CodeSystem:Scenarios:0:ModuleKey"] = "wms",
        ["CodeSystem:Scenarios:0:ObjectType"] = "入库单",
        ["CodeSystem:Scenarios:1:RuleKey"] = "master.material",
        ["CodeSystem:Scenarios:1:ModuleKey"] = "master",
        ["CodeSystem:Scenarios:1:ObjectType"] = "物料"
    };

    [Theory]
    [InlineData("day", "2026-11-09T15:59:59Z", "20261109")]
    [InlineData("day", "2026-11-09T16:00:00Z", "20261110")]
    [InlineData("month", "2026-11-30T16:00:00Z", "202612")]
    [InlineData("year", "2026-12-31T16:00:00Z", "2027")]
    [InlineData("none", "2026-12-31T16:00:00Z", "ALL")]
    public void Rendering_uses_one_business_instant(string reset, string instant, string expected)
    {
        var d = Daily with { Reset = reset };
        d.Validate();
        Assert.Equal(expected, d.Render(DateTimeOffset.Parse(instant), 7).PeriodKey);
    }

    [Fact]
    public void Malformed_segments_and_incompatible_revisions_are_rejected()
    {
        foreach (var d in new[]{ Daily with {Segments=[]}, Daily with {Segments=[new("sequence",Width:2),new("sequence",Width:2)]},
            Daily with {Reset="month",Segments=[new("sequence",Width:4)]}, Simple(13), Simple(2) with {Step=0},
            Simple(3) with {Segments=[new("literal",""),new("sequence",Width:3)]},
            Simple(3) with {Segments=[new("custom"),new("sequence",Width:3)]},
            Simple(3) with {Segments=[new("literal",new string('X',32)),new("literal",new string('X',32)),new("sequence",Width:3)]}})
            Assert.Equal("invalid_rule", Assert.Throws<BusinessException>(() => d.Validate()).Code);
        foreach (var d in new[]{Daily with {Start=2},Daily with {Step=2},Daily with {Reset="month"},
            Daily with {Segments=[new("year"),new("month"),new("day"),new("sequence",Width:3)]}})
            Assert.Throws<BusinessException>(() => d.Validate(Daily));
    }

    [Fact]
    public async Task Management_http_lifecycle_preview_query_audit_and_scopes()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true, businessConfiguration: Scenarios(),
            identityConfiguration: new Dictionary<string, string?> { ["CodeSystem:Modules:0"] = "wms", ["CodeSystem:Modules:1"] = "master" });
        using var client = factory.CreateClient();
        await Problem(await client.PostAsJsonAsync(Root + "/rules/query", new PageQuery()), HttpStatusCode.Unauthorized, "unauthorized");
        await Login(client);
        var preview = new PreviewCode("wms.receipt", Daily, "2026-03-07T09:00:00+08:00", 7);
        for (var i = 0; i < 2; i++)
            Assert.Equal("RK-20260307-0007-A", (await Data<CodePreview>(await client.PostAsJsonAsync(Root + "/preview", preview))).Code);
        await Problem(await client.PostAsJsonAsync(Root + "/preview", preview with { SampleAt = "2026-03-07" }), HttpStatusCode.BadRequest, "invalid_request");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
        Assert.Empty(db.Set<CodeCounter>()); Assert.Empty(db.Set<CodeIssue>());
        var create = new CreateCodeRule("wms.receipt", "入库单", Daily);
        await Problem(await client.PostAsJsonAsync(Root + "/rules", new { create.RuleKey, create.Name, create.Definition, actorId = Guid.NewGuid() }),
            HttpStatusCode.BadRequest, "invalid_request");
        await Problem(await client.PostAsJsonAsync(Root + "/rules", create with { Definition = Simple(0) }), HttpStatusCode.BadRequest, "invalid_rule");
        var rule = await Data<CodeChangeResult>(await client.PostAsJsonAsync(Root + "/rules", create), HttpStatusCode.Created);
        var url = $"{Root}/rules/{rule.Id}";
        await Data<CodeChangeResult>(await client.PostAsJsonAsync(Root + "/rules", create with { RuleKey = "master.material" }), HttpStatusCode.Created);
        await Problem(await client.PostAsJsonAsync(Root + "/rules", create), HttpStatusCode.Conflict, "rule_key_exists");
        await Problem(await client.PostAsJsonAsync(url + "/publish", new CodeTransition(1, " ")), HttpStatusCode.BadRequest, "invalid_request");
        await Data<CodeChangeResult>(await client.PostAsJsonAsync(url + "/publish", new CodeTransition(1, "首发")));
        var generator = scope.ServiceProvider.GetRequiredService<ICodeGenerator>();
        var (request, obj, actor) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var first = await generator.IssueAsync("wms.receipt", request, obj, actor);
        Assert.Equal(1, first.SequenceValue);
        await Problem(await client.PutAsJsonAsync(url + "/draft", new SaveCodeDraft(1, Daily)), HttpStatusCode.Conflict, "revision_conflict");
        await Data<CodeChangeResult>(await client.PostAsJsonAsync(url + "/disable", new CodeTransition(2, "暂停")));
        await Data<CodeChangeResult>(await client.PutAsJsonAsync(url + "/draft", new SaveCodeDraft(3, Daily)));
        Assert.Equal("disabled", (await Data<CodeChangeResult>(await client.PostAsJsonAsync(url + "/publish", new CodeTransition(4, "停用期改版")))).Status);
        Assert.Equal(first, await generator.IssueAsync("wms.receipt", request, obj, actor));
        Assert.Equal("rule_disabled", (await Assert.ThrowsAsync<BusinessException>(() => generator.IssueAsync("wms.receipt", Guid.NewGuid(), Guid.NewGuid(), actor))).Code);
        await Data<CodeChangeResult>(await client.PostAsJsonAsync(url + "/enable", new CodeTransition(5, "恢复")));
        Assert.Equal(2, (await generator.IssueAsync("wms.receipt", Guid.NewGuid(), Guid.NewGuid(), actor)).SequenceValue);
        var q = new PageQuery(PageSize: 1, Sorts: [new("name"), new("ruleKey")]);
        var page = await Data<PageResult<RuleRow>>(await client.PostAsJsonAsync(Root + "/rules/query", q));
        Assert.Equal(2, page.Total);
        Assert.NotEqual(page.Items.Single().Id, (await Data<PageResult<RuleRow>>(await client.PostAsJsonAsync(Root + "/rules/query", q with { Page = 2 }))).Items.Single().Id);
        var filtered = await Data<PageResult<RuleRow>>(await client.PostAsJsonAsync(Root + "/rules/query", q with
        {
            Filters = [
            new("moduleKey","wms","eq"),new("status",Operator:"in",Values:["enabled","disabled"])]
        }));
        Assert.Equal(rule.Id, Assert.Single(filtered.Items).Id);
        Assert.Equal(2, (await Data<PageResult<VersionRow>>(await client.PostAsJsonAsync(url + "/versions/query", new PageQuery()))).Total);
        Assert.Equal("首发", (await Data<VersionDetail>(await client.GetAsync(url + "/versions/1"))).Reason);
        Assert.Equal(6, (await Data<PageResult<EventRow>>(await client.PostAsJsonAsync(url + "/events/query", new PageQuery()))).Total);
        Assert.Equal(2, (await Data<PageResult<IssueRow>>(await client.PostAsJsonAsync(url + "/issues/query", new PageQuery()))).Total);
        var detail = await Data<RuleDetail>(await client.GetAsync(url));
        Assert.Null(detail.Draft); Assert.NotNull(detail.Active);
        var openApi = await factory.CreateBusinessClient().GetStringAsync("/openapi/v1.json");
        Assert.Contains("/api/v1/code-system/rules/{id}/publish", openApi);
        Assert.DoesNotContain("/internal/code-system/rules", openApi);

        await StudyUser(client, "scoped", "code.scope.master.view");
        using var user = factory.CreateClient(); await Login(user, "scoped", "StrongPassword123");
        var authorized = await Data<Actor>(await user.GetAsync("/api/v1/auth/permissions"));
        Assert.Contains("code.rules.view", authorized.Permissions);
        Assert.Equal("master", (await Data<PageResult<CodeScenario>>(await user.PostAsJsonAsync(Root + "/scenarios/query", new PageQuery()))).Items.Single().ModuleKey);
        await Problem(await user.GetAsync(url), HttpStatusCode.NotFound, "rule_not_found");
        await Problem(await user.PostAsJsonAsync(url + "/publish", new CodeTransition(6, "越权")), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await user.PostAsJsonAsync(Root + "/rules/query", q with { Filters = [new("name", "不存在"), new("id", "bad", "eq")] }), HttpStatusCode.BadRequest, "invalid_query");
        await Problem(await user.PostAsJsonAsync(Root + "/rules/query", q with { Filters = [new("status", Operator: "in", Values: ["未知"])] }), HttpStatusCode.BadRequest, "invalid_query");
        await StudyUser(client, "mixed", "code.scope.master.publish", "code.scope.wms.view");
        using var mixed = factory.CreateClient();
        await Login(mixed, "mixed", "StrongPassword123");
        await Data<RuleDetail>(await mixed.GetAsync(url));
        await Problem(await mixed.PostAsJsonAsync(url + "/disable", new CodeTransition(6, "越权停用")), HttpStatusCode.NotFound, "rule_not_found");
        await Problem(await mixed.PostAsJsonAsync(url + "/events/query", new PageQuery()), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Across_midnight_a_replay_recovers_the_original_period_and_new_objects_restart_at_the_configured_start()
    {
        using var factory = new DevkitApiFactory();
        using var scope = factory.Services.CreateScope();
        var clock = new AdjustableClock { Now = DateTimeOffset.Parse("2026-11-09T15:59:59Z") };
        var ledger = new CodeLedger(scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), clock);
        var actor = Guid.NewGuid();
        var scenario = new CodeScenario("wms.receipt", "wms", "入库单");
        var rule = await ledger.CreateAsync(scenario, "入库", Daily with { Start = 10, Step = 5 }, actor, false, default);
        await ledger.ChangeAsync(rule.Id, ["wms"], 1, "publish", null, "启用", actor, default);
        var req = Guid.NewGuid();
        var obj = Guid.NewGuid();
        Task<CodeIssue> Issue(Guid request, Guid business) => ledger.IssueAsync(scenario.RuleKey, scenario.ModuleKey, request, business, actor, default);
        var first = await Issue(req, obj);
        Assert.Equal("RK-20261109-0010-A", first.Code);
        Assert.Equal("RK-20261109-0015-A", (await Issue(Guid.NewGuid(), Guid.NewGuid())).Code);
        clock.Now = clock.Now.AddSeconds(1);
        Assert.Equal(first.Id, (await Issue(req, obj)).Id);
        Assert.Equal("RK-20261110-0010-A", (await Issue(Guid.NewGuid(), Guid.NewGuid())).Code);
    }

    private sealed class AdjustableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Collision_or_exhaustion_rolls_back_and_corrected_draft_resumes(bool collision)
    {
        using var factory = new DevkitApiFactory(businessConfiguration: Scenarios());
        using var scope = factory.Services.CreateScope();
        var ledger = scope.ServiceProvider.GetRequiredService<CodeLedger>();
        var generator = scope.ServiceProvider.GetRequiredService<ICodeGenerator>();
        var actor = Guid.NewGuid();
        var d = collision ? Simple(1) with { Segments = [new("sequence", Width: 1), new("literal", "2")] } : Simple(2, 99);
        var rule = await ledger.CreateAsync(new("wms.receipt", "wms", "入库单"), "入库", d, actor, false, default);
        async Task Change(long revision, string action, CodeDefinition? draft = null) => await ledger.ChangeAsync(rule.Id, ["wms"], revision, action, draft, "升级", actor, default);
        await Change(1, "publish");
        var first = await generator.IssueAsync("wms.receipt", Guid.NewGuid(), Guid.NewGuid(), actor);
        var rev = 2;
        if (collision)
        {
            await Change(rev++, "save_draft", d with { Segments = [new("literal", "1"), new("sequence", Width: 1)] });
            await Change(rev++, "publish");
        }
        var (req, obj) = (Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(collision ? "code_collision" : "sequence_exhausted", (await Assert.ThrowsAsync<BusinessException>(() => generator.IssueAsync("wms.receipt", req, obj, actor))).Code);
        var db = scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
        Assert.Equal(first.SequenceValue, (await db.Set<CodeCounter>().AsNoTracking().SingleAsync()).LastValue);
        await Change(rev++, "save_draft", d with { Segments = [new("literal", "B"), new("sequence", Width: 3)] });
        await Change(rev, "publish");
        var next = await generator.IssueAsync("wms.receipt", req, obj, actor);
        Assert.Equal(first.SequenceValue + 1, next.SequenceValue);
        Assert.Equal(next, await generator.IssueAsync("wms.receipt", req, obj, actor));
        Assert.Equal("idempotency_conflict", (await Assert.ThrowsAsync<BusinessException>(() => generator.IssueAsync("wms.receipt", req, Guid.NewGuid(), actor))).Code);
        Assert.Equal("business_already_numbered", (await Assert.ThrowsAsync<BusinessException>(() => generator.IssueAsync("wms.receipt", Guid.NewGuid(), obj, actor))).Code);
    }
}
