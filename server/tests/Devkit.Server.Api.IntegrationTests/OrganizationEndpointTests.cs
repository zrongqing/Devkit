using System.Net;
using System.Net.Http.Json;
using Devkit.Application.Domain.Organization;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class OrganizationEndpointTests
{
    private const string Root = "/api/v1/identity/organization";
    private const string Audit = "/api/v1/identity/audit";

    [Fact]
    public async Task Directory_enforces_hierarchy_and_preserves_occupied_positions()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateIdentityClient();
        var actor = await Login(client);
        var departments = Root + "/departments";
        var positions = Root + "/positions";
        await Problem(await client.PostAsJsonAsync(departments, new { name = " " }), HttpStatusCode.BadRequest, "invalid_name");
        var parent = await Data<Guid>(await client.PostAsJsonAsync(departments, new { name = "总部" }));
        var child = await Data<Guid>(await client.PostAsJsonAsync(departments, new { name = "研发", parentId = parent }));
        await Problem(await client.PutAsJsonAsync($"{departments}/{parent}", new { name = "总部", parentId = child }), HttpStatusCode.Conflict, "organization_conflict");
        await Problem(await client.PostAsJsonAsync(departments, new { name = "研发", parentId = parent }), HttpStatusCode.Conflict, "organization_conflict");
        var position = await Data<Guid>(await client.PostAsJsonAsync(positions, new { name = "工程师", departmentId = child }));
        await Problem(await client.DeleteAsync($"{departments}/{child}"), HttpStatusCode.Conflict, "organization_conflict");
        var members = $"{positions}/{position}/members";
        await Problem(await client.PutAsync($"{members}/{Guid.NewGuid()}", null), HttpStatusCode.NotFound, "not_found");
        var assignment = $"{members}/{actor.User.Id}";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(assignment, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(assignment, null)).StatusCode);
        var directory = await Data<DirectorySnapshot>(await client.GetAsync(Root));
        Assert.Equal(new Occupancy(actor.User.Id, position), Assert.Single(directory.Members));
        Assert.Contains(directory.Departments, d => d.Id == child && d.ParentId == parent);
        await Problem(await client.DeleteAsync($"{positions}/{position}"), HttpStatusCode.Conflict, "organization_conflict");
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(assignment)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{positions}/{position}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{departments}/{child}")).StatusCode);
        var events = await Data<AdministrationEvent[]>(await client.GetAsync(Audit));
        Assert.Single(events, e => e.ActorId == actor.User.Id && e.Operation == "PUT " + assignment);
        var older = await Data<AdministrationEvent[]>(await client.GetAsync($"{Audit}?before={events[1].Id}"));
        Assert.Equal(events.Skip(2).Select(e => e.Id), older.Select(e => e.Id));
        await Problem(await client.GetAsync(Audit + "?before=0"), HttpStatusCode.BadRequest, "invalid_cursor");
    }

    [Fact]
    public async Task Directory_and_audit_have_independent_permissions()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateClient();
        await Problem(await client.GetAsync(Root), HttpStatusCode.Unauthorized, "unauthorized");
        await Problem(await client.GetAsync(Audit), HttpStatusCode.Unauthorized, "unauthorized");
        using var admin = factory.CreateClient();
        await Login(admin);
        await StudyUser(admin, "auditor", "system.audit.view");
        await Login(client, "auditor", "StrongPassword123");
        Assert.NotEmpty(await Data<AdministrationEvent[]>(await client.GetAsync(Audit)));
        await Problem(await client.GetAsync(Root), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await client.PostAsJsonAsync(Root + "/departments", new { name = "财务" }), HttpStatusCode.Forbidden, "forbidden");
        await StudyUser(admin, "organizer", "system.organization.manage");
        await Login(client, "organizer", "StrongPassword123");
        await Data<Guid>(await client.PostAsJsonAsync(Root + "/departments", new { name = "财务" }));
        await Problem(await client.GetAsync(Audit), HttpStatusCode.Forbidden, "forbidden");
    }
}
