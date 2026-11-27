using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Devkit.Application.Domain.Organization;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class SystemSettingsEndpointTests
{
    private const string Path = "/api/v1/identity/system-settings";

    [Fact]
    public async Task Display_time_zone_defaults_to_browser_and_changes_are_shared_and_audited()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var reader = factory.CreateClient();
        using var admin = factory.CreateIdentityClient();

        using var initial = await reader.GetAsync(Path);
        Assert.True(initial.Headers.CacheControl?.NoStore);
        var data = await Data<JsonElement>(initial);
        Assert.Equal(JsonValueKind.Null, Assert.Single(data.EnumerateObject()).Value.ValueKind);
        Assert.Null(data.GetProperty("timeZone").GetString());

        var actor = await Login(admin);
        Assert.Equal("Asia/Shanghai", await Update(admin, "Asia/Shanghai"));
        Assert.Equal("Asia/Shanghai", await Read(reader));
        Assert.Equal("UTC", await Update(admin, "UTC"));
        Assert.Equal("UTC", await Read(reader));
        Assert.Null(await Update(admin, null));
        Assert.Null(await Read(reader));

        var audit = await Data<AdministrationEvent[]>(await admin.GetAsync("/api/v1/identity/audit"));
        var changes = audit.Where(e => e.Operation == "PUT " + Path).ToArray();
        Assert.Equal(3, changes.Length);
        Assert.All(changes, change => Assert.Equal(actor.User.Id, change.ActorId));
    }

    [Fact]
    public async Task Only_the_settings_permission_can_write_and_rejected_writes_leave_the_value_intact()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true);
        using var client = factory.CreateIdentityClient();
        using var admin = factory.CreateIdentityClient();
        await Problem(await client.PutAsJsonAsync(Path, new { timeZone = "Europe/London" }), HttpStatusCode.Unauthorized, "unauthorized");

        await Login(admin);
        await StudyUser(admin, "settings-manager", "system.settings.manage");
        await StudyUser(admin, "storage-manager", "system.storage.manage");
        await Login(client, "settings-manager", "StrongPassword123");
        Assert.Equal("America/New_York", await Update(client, "America/New_York"));

        await Login(client, "storage-manager", "StrongPassword123");
        Assert.Equal("America/New_York", await Read(client));
        await Problem(await client.PutAsJsonAsync(Path, new { timeZone = "Europe/London" }), HttpStatusCode.Forbidden, "forbidden");

        foreach (var zone in new[] { "", " ", "+08:00", "Etc/GMT-8", "China Standard Time", "Mars/Olympus", " Asia/Shanghai" })
            await Problem(await admin.PutAsJsonAsync(Path, new { timeZone = zone }), HttpStatusCode.BadRequest, "invalid_time_zone");

        foreach (var json in new[] { "{}", "null", "{\"timeZone\":123}", "{\"timeZone\":" })
        {
            using var body = new StringContent(json, Encoding.UTF8, "application/json");
            await Problem(await admin.PutAsync(Path, body), HttpStatusCode.BadRequest, "invalid_json");
        }

        Assert.Equal("America/New_York", await Read(client));
        var audit = await Data<AdministrationEvent[]>(await admin.GetAsync("/api/v1/identity/audit"));
        Assert.Single(audit, change => change.Operation == "PUT " + Path);
    }

    [Fact]
    public async Task Settings_operations_are_described_in_the_v1_contract()
    {
        using var factory = new DevkitApiFactory();
        using var client = factory.CreateIdentityClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var path = document.GetProperty("paths").GetProperty(Path);
        Assert.True(path.GetProperty("get").GetProperty("responses").TryGetProperty("200", out _));
        var put = path.GetProperty("put");
        Assert.True(put.GetProperty("requestBody").GetProperty("required").GetBoolean());
        var responses = put.GetProperty("responses");
        foreach (var status in new[] { "200", "400", "401", "403" })
            Assert.True(responses.TryGetProperty(status, out _));
    }

    private static async Task<string?> Read(HttpClient client) =>
        (await Data<JsonElement>(await client.GetAsync(Path))).GetProperty("timeZone").GetString();

    private static async Task<string?> Update(HttpClient client, string? timeZone) =>
        (await Data<JsonElement>(await client.PutAsJsonAsync(Path, new { timeZone }))).GetProperty("timeZone").GetString();
}
