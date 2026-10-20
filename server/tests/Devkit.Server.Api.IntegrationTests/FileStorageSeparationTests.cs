using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Devkit.Business.Application.Workspace;
using Devkit.Business.Domain.Workspace;
using Devkit.Business.Infrastructure.Persistence;
using Devkit.Business.Infrastructure.Workspace;
using Devkit.FileStorage.Domain.Files;
using Devkit.FileStorage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Devkit.Server.Api.IntegrationTests.WorkspaceTestSupport;

namespace Devkit.Server.Api.IntegrationTests;
public sealed class FileStorageSeparationTests
{
    [Fact]
    public void Business_extraction_migration_preserves_legacy_file_tables_for_offline_transfer()
    {
        var migration=new Devkit.Business.Infrastructure.Persistence.Migrations.SqlServer.ExtractFileStorageContext();
        Assert.DoesNotContain(migration.UpOperations,operation=>operation is Microsoft.EntityFrameworkCore.Migrations.Operations.DropTableOperation);
        Assert.Contains(migration.UpOperations,operation=>operation is Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation{ Name:"FileReleaseOutbox" });
        Assert.All(migration.DownOperations,operation=>Assert.Equal("FileReleaseOutbox",Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.DropTableOperation>(operation).Name));
    }
    private static async Task<Guid> Upload(HttpClient client)
    {
        using var form=new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4\n")),"file","sample.pdf");
        var result=await Data<JsonElement>(await client.PostAsync("/api/v1/files/?purpose=exam-study",form));
        return result.GetProperty("id").GetGuid();
    }
    [Fact]
    public async Task File_host_owns_routes_storage_tables_and_jobs_without_business_or_identity_assemblies()
    {
        using var factory=new DevkitApiFactory();using var file=factory.CreateFileClient();using var business=factory.CreateBusinessClient();
        Assert.Equal(HttpStatusCode.NotFound,(await business.GetAsync("/api/v1/files/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await business.GetAsync("/api/v1/storage/jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await file.GetAsync("/api/v1/auth/login")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await file.GetAsync("/api/v1/exam-study/jobs")).StatusCode);
        var paths=(await file.GetFromJsonAsync<JsonElement>("/openapi/v1.json")).GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/files",out var fileOperations), paths.ToString());
        Assert.True(fileOperations.TryGetProperty("post",out _));
        Assert.True(paths.TryGetProperty("/api/v1/storage/jobs",out _));
        Assert.False(paths.TryGetProperty("/api/v1/exam-study/jobs",out _));
        using var fs=factory.FileServices.CreateScope();using var bs=factory.Services.CreateScope();
        var fileDb=fs.ServiceProvider.GetRequiredService<FileStorageDbContext>();var businessDb=bs.ServiceProvider.GetRequiredService<BusinessDbContext>();
        Assert.NotEqual(fileDb.Database.GetConnectionString(),businessDb.Database.GetConnectionString());
        Assert.DoesNotContain(businessDb.Model.GetEntityTypes(),x=>x.GetTableName() is "StoredFiles" or "StorageLocations" or "FileReferences" or "FileMigrationItems" or "StorageJobs");
        Assert.DoesNotContain(fileDb.Model.GetEntityTypes(),x=>x.GetTableName() is "StudySources" or "WorkspaceJobs" or "Users" or "RefreshTokens");
        Assert.Contains(fileDb.Database.GetMigrations(),x=>x.EndsWith("InitialFileStorageContext"));
        foreach(var assembly in new[]{typeof(Devkit.FileStorage.Api.Program).Assembly,typeof(FileStorageDbContext).Assembly,
            typeof(Devkit.FileStorage.Application.Files.Actor).Assembly,typeof(StoredFile).Assembly})
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(),x=>x.Name!.StartsWith("Devkit.Business.")||x.Name.StartsWith("Devkit.Identity.")||x.Name=="UglyToad.PdfPig"||x.Name=="DocumentFormat.OpenXml");
        foreach(var assembly in new[]{typeof(Devkit.Business.Api.Program).Assembly,typeof(BusinessDbContext).Assembly,typeof(StudyService).Assembly,typeof(SourceVersion).Assembly})
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(),x=>x.Name!.StartsWith("Devkit.FileStorage."));
    }
    [Fact]
    public async Task Public_file_routes_require_current_identity_and_registered_permissions()
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true);using var gateway=factory.CreateClient();using var file=factory.CreateFileClient();
        await Problem(await file.GetAsync("/api/v1/files/"),HttpStatusCode.Unauthorized,"unauthorized");
        await Login(gateway);await factory.InitializeStorageAsync();var fileId=await Upload(gateway);
        await StudyUser(gateway,"reader");using var reader=factory.CreateClient();var pair=await Login(reader,"reader","StrongPassword123");
        using var direct=factory.CreateFileClient();direct.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",pair.AccessToken);
        await Problem(await direct.GetAsync("/api/v1/files/"),HttpStatusCode.Forbidden,"forbidden");
        await Problem(await direct.GetAsync($"/api/v1/files/{fileId}/content"),HttpStatusCode.NotFound,"not_found");
        (await reader.PostAsync("/api/v1/auth/logout-all",null)).EnsureSuccessStatusCode();
        await Problem(await direct.GetAsync("/api/v1/storage/jobs"),HttpStatusCode.Unauthorized,"unauthorized");
    }
    [Fact]
    public async Task Internal_file_access_requires_client_key_current_user_and_a_reference_in_the_client_module()
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true,fileConfiguration:new Dictionary<string,string?>
        { ["FileStorage:Clients:Other:ApiKey"]=DevkitApiFactory.FileClientKey,["FileStorage:Clients:Other:Module"]="general" });
        using var gateway=factory.CreateClient();await Login(gateway);await factory.InitializeStorageAsync();var fileId=await Upload(gateway);
        using var direct=factory.CreateFileClient();var entityId=Guid.NewGuid();
        await Problem(await direct.GetAsync($"/api/v1/internal/files/{fileId}/content?entityId={entityId}"),HttpStatusCode.Unauthorized,"service_unauthorized");
        direct.DefaultRequestHeaders.Add("X-File-Service","Business");direct.DefaultRequestHeaders.Add("X-File-Key",DevkitApiFactory.FileClientKey);
        await Problem(await direct.PutAsJsonAsync($"/api/v1/internal/files/{fileId}/references/{entityId}",new {ownerId=Guid.NewGuid()}),HttpStatusCode.Unauthorized,"unauthorized");
        var kb=await Base(gateway);var source=await Source(factory,gateway,kb.Id,"人工校正文",fileId);entityId=source.Versions[0].Id;
        Assert.Equal(HttpStatusCode.OK,(await direct.GetAsync($"/api/v1/internal/files/{fileId}/content?entityId={entityId}")).StatusCode);
        direct.DefaultRequestHeaders.Remove("X-File-Service");direct.DefaultRequestHeaders.Add("X-File-Service","Other");
        await Problem(await direct.GetAsync($"/api/v1/internal/files/{fileId}/content?entityId={entityId}"),HttpStatusCode.NotFound,"not_found");
        (await direct.DeleteAsync($"/api/v1/internal/files/references/{entityId}")).EnsureSuccessStatusCode();
        Assert.Single(await Data<FileReference[]>(await gateway.GetAsync($"/api/v1/files/{fileId}/references")));
    }
    [Theory]
    [InlineData("network")]
    [InlineData("timeout")]
    [InlineData("redirect")]
    [InlineData("bad-key")]
    public async Task Business_file_dependency_failures_are_503_and_do_not_create_sources(string failure)
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true,businessConfiguration:failure=="bad-key"?new Dictionary<string,string?>{["FileService:ApiKey"]=new string('x',40)}:null);
        using var gateway=factory.CreateClient();await Login(gateway);await factory.InitializeStorageAsync();var fileId=await Upload(gateway);var kb=await Base(gateway);
        if(failure!="bad-key")factory.FileOverride=(_,_)=>failure switch
        {
            "network"=>throw new HttpRequestException("isolated offline"),
            "timeout"=>throw new TaskCanceledException("isolated timeout"),
            _=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect))
        };
        await Problem(await gateway.PostAsJsonAsync($"/api/v1/exam-study/knowledge-bases/{kb.Id}/sources",new SourceRequest("test",fileId)),HttpStatusCode.ServiceUnavailable,"file_service_unavailable");
        using var scope=factory.Services.CreateScope();Assert.Empty(await scope.ServiceProvider.GetRequiredService<BusinessDbContext>().Set<SourceVersion>().ToListAsync());
        // Failure never invalidates the user's session or stops ordinary business operations.
        Assert.Equal(HttpStatusCode.OK,(await gateway.GetAsync("/api/v1/exam-study/knowledge-bases")).StatusCode);
    }
    [Fact]
    public async Task Background_file_download_deadline_is_reported_as_dependency_unavailable()
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true,businessConfiguration:new Dictionary<string,string?>{["FileService:TimeoutSeconds"]="1"});
        using var gateway=factory.CreateClient();await Login(gateway);await factory.InitializeStorageAsync();
        var fileId=await Upload(gateway);var kb=await Base(gateway);
        var job=await Data<WorkJob>(await gateway.PostAsJsonAsync($"/api/v1/exam-study/knowledge-bases/{kb.Id}/sources",new SourceRequest("download",fileId)),HttpStatusCode.Accepted);
        factory.FileOverride=async(_,ct)=>{await Task.Delay(TimeSpan.FromSeconds(3),ct);return new HttpResponseMessage(HttpStatusCode.OK);};
        var error=await Assert.ThrowsAsync<BusinessException>(()=>factory.RunJobAsync(job.Id));
        Assert.Equal(503,error.Status);Assert.Equal("file_service_unavailable",error.Code);
    }
    [Fact]
    public async Task A_failed_source_commit_keeps_durable_compensation_and_releases_orphan_reference_after_recovery()
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true);using var gateway=factory.CreateClient();await Login(gateway);await factory.InitializeStorageAsync();
        var fileId=await Upload(gateway);var kb=await Base(gateway);factory.FailSourceCommit=true;
        factory.FileOverride=(request,_)=>request.Method==HttpMethod.Delete?Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)):Forward();
        async Task<HttpResponseMessage> Forward()
        {
            // The test boundary lets the PUT succeed while its compensation is temporarily unavailable.
            using var client=factory.CreateFileClient();using var request=new HttpRequestMessage(HttpMethod.Put,$"/api/v1/internal/files/{fileId}/references/{CurrentIntent()}");
            request.Headers.Add("X-File-Service","Business");request.Headers.Add("X-File-Key",DevkitApiFactory.FileClientKey);
            request.Headers.Authorization=gateway.DefaultRequestHeaders.Authorization;
            using var scope=factory.IdentityServices.CreateScope();
            var userId=(await scope.ServiceProvider.GetRequiredService<Devkit.Identity.Infrastructure.Persistence.IdentityDbContext>().Users.SingleAsync(x=>x.UserName=="admin")).Id;
            request.Content=JsonContent.Create(new{ownerId=userId});return await client.SendAsync(request);
        }
        Guid CurrentIntent(){using var s=factory.Services.CreateScope();return s.ServiceProvider.GetRequiredService<BusinessDbContext>().Set<FileRelease>().Single().EntityId;}
        Assert.Equal(HttpStatusCode.InternalServerError,(await gateway.PostAsJsonAsync($"/api/v1/exam-study/knowledge-bases/{kb.Id}/sources",new SourceRequest("will rollback",fileId))).StatusCode);
        Assert.Single(await Data<FileReference[]>(await gateway.GetAsync($"/api/v1/files/{fileId}/references")));
        factory.FileOverride=null;factory.FailSourceCommit=false;
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<BusinessDbContext>();Assert.False(await db.Set<SourceVersion>().AnyAsync());
            var intent=await db.Set<FileRelease>().SingleAsync();intent.NotBeforeUtc=DateTime.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<RemoteFileService>().ReconcileAsync(CancellationToken.None);
        }
        Assert.Empty(await Data<FileReference[]>(await gateway.GetAsync($"/api/v1/files/{fileId}/references")));
    }
    [Fact]
    public async Task Deleted_source_releases_are_committed_locally_and_retried_when_file_service_recovers()
    {
        using var factory=new DevkitApiFactory(builtInAdmin:true);using var gateway=factory.CreateClient();await Login(gateway);await factory.InitializeStorageAsync();
        var fileId=await Upload(gateway);var kb=await Base(gateway);var source=await Source(factory,gateway,kb.Id,"manual",fileId);
        // An uncertain commit must retain a reference when the source version actually exists.
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
            db.Add(new FileRelease{EntityId=source.Versions[0].Id,KeepIfVersionExists=true,NotBeforeUtc=DateTime.UtcNow.AddMinutes(-1)});
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<RemoteFileService>().CancelLinkAsync(source.Versions[0].Id,CancellationToken.None);
        }
        Assert.Single(await Data<FileReference[]>(await gateway.GetAsync($"/api/v1/files/{fileId}/references")));
        factory.FileOverride=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        (await gateway.DeleteAsync($"/api/v1/exam-study/sources/{source.Source.Id}")).EnsureSuccessStatusCode();
        Assert.Single(await Data<FileReference[]>(await gateway.GetAsync($"/api/v1/files/{fileId}/references")));
        factory.FileOverride=null;
        using(var scope=factory.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<RemoteFileService>().ReconcileAsync(CancellationToken.None);
        Assert.Empty(await Data<FileReference[]>(await gateway.GetAsync($"/api/v1/files/{fileId}/references")));
    }
}
