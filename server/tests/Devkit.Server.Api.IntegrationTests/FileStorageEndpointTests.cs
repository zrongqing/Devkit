using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.FileStorage;
using Devkit.Server.Domain.Workspace;
using Devkit.Server.Infrastructure.Persistence;
using Devkit.Server.Infrastructure.Workspace;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class FileStorageEndpointTests
{
    private static async Task<HttpResponseMessage> Upload(HttpClient client, string name, byte[] bytes, string purpose = "exam-study")
    {
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(bytes), "file", name);
        return await client.PostAsync($"/api/v1/files/?purpose={purpose}", multipart);
    }

    [Fact]
    public async Task Docx_original_is_retained_referenced_extracted_and_explicitly_purged()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var client = factory.CreateClient(); await Login(client); await factory.InitializeStorageAsync();
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var part = doc.AddMainDocumentPart();
            part.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text("第一条 请假需要提前一天申请。")))));
        }
        var bytes = stream.ToArray(); var file = await Data<FileView>(await Upload(client, "考勤制度.docx", bytes));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), file.Sha256);
        var kb = await Base(client); var source = await Source(factory, client, kb.Id, "", file.Id);
        Assert.Contains("请假需要提前一天", source.Versions[0].Text); Assert.NotEmpty(source.Chunks);
        Assert.Single(await Data<FileReference[]>(await client.GetAsync($"/api/v1/files/{file.Id}/references")));
        await Problem(await client.DeleteAsync($"/api/v1/files/{file.Id}"), HttpStatusCode.Conflict, "file_referenced");
        await Data<SuccessView>(await client.DeleteAsync($"/api/v1/exam-study/sources/{source.Source.Id}"));
        var download = await client.GetAsync($"/api/v1/files/{file.Id}/content");
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", Assert.Single(download.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("考勤制度.docx", download.Content.Headers.ContentDisposition!.FileNameStar);
        Assert.Empty(await Data<FileReference[]>(await client.GetAsync($"/api/v1/files/{file.Id}/references")));
        await Data<SuccessView>(await client.DeleteAsync($"/api/v1/files/{file.Id}"));
        await Problem(await client.GetAsync($"/api/v1/files/{file.Id}/content"), HttpStatusCode.Conflict, "file_unavailable");
    }

    [Theory]
    [InlineData("legacy.doc", "test", 415, "unsupported_file")]
    [InlineData("fake.pdf", "not a pdf", 415, "invalid_file_content")]
    [InlineData("empty.pdf", "", 400, "empty_file")]
    public async Task Invalid_uploads_are_rejected_without_ready_files(string name, string content, int status, string code)
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var client = factory.CreateClient(); await Login(client); await factory.InitializeStorageAsync();
        await Problem(await Upload(client, name, Encoding.UTF8.GetBytes(content)), (HttpStatusCode)status, code);
        Assert.DoesNotContain(await Data<FileView[]>(await client.GetAsync("/api/v1/files/")), x => x.Status == "ready");
    }

    [Fact]
    public async Task Upload_limits_are_enforced_and_other_users_cannot_download_or_purge()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var admin = factory.CreateClient(); await Login(admin); await factory.InitializeStorageAsync();
        await StudyUser(admin, "reader", "system.files.manage");
        var options = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<WorkspaceOptions>>().Value; options.MaximumUploadBytes = 16;
        await Problem(await Upload(admin, "oversized.pdf", Encoding.UTF8.GetBytes("%PDF-" + new string('A', 32))), HttpStatusCode.RequestEntityTooLarge, "file_too_large");
        var file = await Data<FileView>(await Upload(admin, "original.pdf", Encoding.UTF8.GetBytes("%PDF-1.4\n")));
        using var reader = factory.CreateClient(); await Login(reader, "reader", "StrongPassword123");
        await Problem(await reader.GetAsync($"/api/v1/files/{file.Id}/content"), HttpStatusCode.NotFound, "not_found");
        await Problem(await reader.DeleteAsync($"/api/v1/files/{file.Id}"), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Migration_preserves_file_id_verifies_copy_and_cleans_old_copy_separately()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var client = factory.CreateClient(); await Login(client); await factory.InitializeStorageAsync();
        var bytes = Encoding.UTF8.GetBytes("permanent original content"); var file = await Data<FileView>(await Upload(client, "资料.txt", bytes, "general"));
        var source = Assert.Single(await Data<StorageView[]>(await client.GetAsync("/api/v1/storage/locations")));
        var target = await Data<StorageLocation>(await client.PostAsJsonAsync("/api/v1/storage/locations", new LocationRequest("扩容目录", Path.Combine(factory.DataRoot, "target"))));
        var job = await Data<JobCreatedView>(await client.PostAsJsonAsync("/api/v1/storage/migrations", new MigrationRequest(source.Id, target.Id)), HttpStatusCode.Accepted);
        await Problem(await client.PostAsync($"/api/v1/storage/migrations/{job.Id}/cleanup", null), HttpStatusCode.Conflict, "storage_busy");
        await factory.RunJobAsync(job.Id); await factory.RunJobAsync(job.Id);
        Assert.Equal(bytes, await (await client.GetAsync($"/api/v1/files/{file.Id}/content")).Content.ReadAsByteArrayAsync());
        string key;
        using (var scope = factory.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<DevkitDbContext>().Set<StoredFile>().SingleAsync(x => x.Id == file.Id);
            Assert.Equal(target.Id, stored.LocationId); key = stored.ObjectKey;
        }
        var oldPath = Path.Combine(source.RootPath, key); Assert.True(File.Exists(oldPath));
        await Data<SuccessView>(await client.PostAsync($"/api/v1/storage/migrations/{job.Id}/cleanup", null));
        var cleanup = (await Data<WorkJob[]>(await client.GetAsync("/api/v1/exam-study/jobs"))).Single(x => x.Kind == "cleanup");
        await Problem(await client.PostAsJsonAsync("/api/v1/storage/migrations", new MigrationRequest(target.Id, source.Id)), HttpStatusCode.Conflict, "migration_busy");
        await factory.RunJobAsync(cleanup.Id);
        Assert.False(File.Exists(oldPath)); Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(target.RootPath, key)));
        Assert.Equal(file.Id, Assert.Single(await Data<FileView[]>(await client.GetAsync("/api/v1/files/"))).Id);
    }

    [Fact]
    public async Task Corrupt_source_never_switches_location_and_retry_can_resume()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var client = factory.CreateClient(); await Login(client); await factory.InitializeStorageAsync();
        var bytes = Encoding.UTF8.GetBytes("original content"); var file = await Data<FileView>(await Upload(client, "record.txt", bytes, "general"));
        var source = Assert.Single(await Data<StorageView[]>(await client.GetAsync("/api/v1/storage/locations")));
        var target = await Data<StorageLocation>(await client.PostAsJsonAsync("/api/v1/storage/locations", new LocationRequest("新位置", Path.Combine(factory.DataRoot, "destination"))));
        var job = await Data<JobCreatedView>(await client.PostAsJsonAsync("/api/v1/storage/migrations", new MigrationRequest(source.Id, target.Id)), HttpStatusCode.Accepted);
        string path;
        using (var scope = factory.Services.CreateScope()) path = Path.Combine(source.RootPath, (await scope.ServiceProvider.GetRequiredService<DevkitDbContext>().Set<StoredFile>().SingleAsync(x => x.Id == file.Id)).ObjectKey);
        await File.WriteAllTextAsync(path, "tampered");
        Assert.Equal("checksum_mismatch", (await Assert.ThrowsAsync<BusinessException>(() => factory.RunJobAsync(job.Id))).Code);
        using (var scope = factory.Services.CreateScope()) Assert.Equal(source.Id, (await scope.ServiceProvider.GetRequiredService<DevkitDbContext>().Set<StoredFile>().SingleAsync(x => x.Id == file.Id)).LocationId);
        await File.WriteAllBytesAsync(path, bytes); await factory.RunJobAsync(job.Id);
        Assert.Equal(bytes, await (await client.GetAsync($"/api/v1/files/{file.Id}/content")).Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Storage_paths_must_be_absolute_nonroot_and_nonoverlapping()
    {
        using var factory = new DevkitApiFactory(builtInAdmin: true); using var client = factory.CreateClient(); await Login(client); await factory.InitializeStorageAsync();
        foreach (var path in new[] { null!, "relative-path", Path.GetPathRoot(factory.DataRoot)! })
            await Problem(await client.PostAsJsonAsync("/api/v1/storage/locations", new LocationRequest("无效位置", path)), HttpStatusCode.BadRequest, "invalid_path");
        await Problem(await client.PostAsJsonAsync("/api/v1/storage/locations", new LocationRequest("重叠位置", Path.Combine(factory.DataRoot, "storage", "nested"))), HttpStatusCode.Conflict, "overlapping_storage");
    }
}
