using System.Net;
using Devkit.Application.Infrastructure.Persistence;
using Devkit.Business.Application.CodeSystem;
using Devkit.Business.Domain.CodeSystem;
using Devkit.Business.Infrastructure.CodeSystem;
using Devkit.Business.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class CodeStartupTests
{
    [Fact]
    public async Task Business_host_loads_deployment_manifest_and_seeds_one_unpublished_draft()
    {
        var config = CodeSystemTests.Scenarios();
        const string seed = "CodeSystem:Drafts:0:";
        config[seed + "RuleKey"] = "wms.receipt";
        config[seed + "Name"] = "入库单";
        foreach (var (field, value) in new Dictionary<string, string>
        {
            ["SchemaVersion"] = "1",
            ["UtcOffset"] = "+08:00",
            ["Reset"] = "none",
            ["Start"] = "1",
            ["Step"] = "1",
            ["Segments:0:Type"] = "sequence",
            ["Segments:0:Width"] = "4"
        })
            config[seed + "Definition:" + field] = value;
        using var factory = new DevkitApiFactory(businessConfiguration: config);
        var worker = factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<Microsoft.Extensions.Hosting.BackgroundService>()
            .Single(x => x.GetType().GenericTypeArguments.Contains(typeof(CodeStartup)));
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
        var rule = await db.Set<CodeRule>().SingleAsync();
        Assert.Equal("入库单", rule.Name);
        Assert.Equal("unpublished", rule.Status);
        Assert.Equal(CodeStartup.SeedActor, rule.CreatedBy);
        Assert.Empty(db.Set<CodeVersion>());
        Assert.Empty(db.Set<CodeIssue>());
    }

    [Fact]
    public async Task Empty_manifests_partial_failure_and_concurrent_restarts_only_fill_missing_items()
    {
        var secret = Guid.NewGuid().ToString("N");
        var roleId = Guid.NewGuid();
        var roles = new Dictionary<string, string?>
        {
            ["CodeSystem:Modules:0"] = "wms",
            ["CodeSystem:BootstrapKey"] = secret,
            ["CodeSystem:Roles:0:Key"] = "editor",
            ["CodeSystem:Roles:0:Id"] = roleId.ToString(),
            ["CodeSystem:Roles:0:Name"] = "编码维护",
            ["CodeSystem:Roles:0:Permissions:0"] = "code.scope.wms.edit"
        };
        using var f = new DevkitApiFactory(identityConfiguration: roles, businessConfiguration: CodeSystemTests.Scenarios());
        using var identity = f.CreateIdentityClient();
        const string prefix = "/api/v1/internal/code-system/bootstrap/roles/";
        await WorkspaceTestSupport.Problem(await identity.PutAsync(prefix + "editor", null), HttpStatusCode.Unauthorized, "service_unauthorized");
        identity.DefaultRequestHeaders.Add("X-Code-Bootstrap-Key", secret);
        await WorkspaceTestSupport.Problem(await identity.PutAsync(prefix + "arbitrary", null), HttpStatusCode.NotFound, "role_not_registered");
        using var scope = f.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var settings = new CodeSettings { Scenarios = [new("wms.receipt", "wms", "入库单")], BootstrapKey = secret };
        var startup = new CodeStartup(settings, sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<CodeLedger>(), NullLogger<CodeStartup>.Instance);
        var db = sp.GetRequiredService<BusinessDbContext>();
        await startup.InitializeAsync(default);
        Assert.Empty(db.Set<CodeRule>());
        settings.RoleKeys = ["editor"];
        settings.Drafts = [new("wms.receipt", "入库", CodeSystemTests.Simple(0))];
        await Assert.ThrowsAsync<Devkit.Business.Application.Workspace.BusinessException>(() => startup.InitializeAsync(default));
        using var identities = f.IdentityServices.CreateScope();
        var users = identities.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var role = await users.Roles.Include(r => r.RoleClaims).SingleAsync(r => r.Id == roleId);
        var grant = Assert.Single(role.RoleClaims);
        Assert.Equal("code.scope.wms.edit", grant.ClaimValue);
        grant.IsDeleted = true;
        role.Name = "人工修改";
        await users.SaveChangesAsync();
        settings.Drafts = [new("wms.receipt", "入库", CodeSystemTests.Simple(4))];
        await Task.WhenAll(startup.InitializeAsync(default), startup.InitializeAsync(default));
        settings.Drafts = [new("wms.receipt", "新配置名称", CodeSystemTests.Simple(3))];
        await startup.InitializeAsync(default);
        var rule = await db.Set<CodeRule>().AsNoTracking().SingleAsync();
        Assert.Equal("入库", rule.Name);
        Assert.Equal("unpublished", rule.Status);
        Assert.Equal(1, rule.Revision);
        Assert.Single(db.Set<CodeEvent>()); Assert.Empty(db.Set<CodeCounter>()); Assert.Empty(db.Set<CodeIssue>());
        Assert.Equal("人工修改", (await users.Roles.AsNoTracking().SingleAsync(r => r.Id == roleId)).Name);
        Assert.False(await users.RoleClaims.AnyAsync(c => c.RoleId == roleId && !c.IsDeleted));
        Assert.False(await users.UserRoles.AnyAsync(c => c.RoleId == roleId));
    }
}
