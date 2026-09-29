using System.Security.Cryptography;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.FileStorage;
using Devkit.Server.Domain.Workspace;
using Devkit.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Devkit.Server.Infrastructure.Workspace;

public sealed class LocalFileService(DevkitDbContext db, IOptions<WorkspaceOptions> configured) : IFileService
{
    private readonly WorkspaceOptions options=configured.Value;
    public async Task EnsureDefaultAsync(CancellationToken ct)
    {
        ValidateRoot(options.DataRoot);
        foreach(var folder in new[]{"files","temp","exports","logs","backups"}) Directory.CreateDirectory(Path.Combine(options.DataRoot,folder));
        if(!await db.Set<StorageLocation>().AnyAsync(ct))
        {
            db.Add(new StorageLocation{Name="默认存储",RootPath=Path.GetFullPath(options.DataRoot),Writable=true});
            await db.SaveChangesAsync(ct);
        }
    }
    public static void ValidateRoot(string root)
    {
        if(string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) throw new BusinessException(400,"invalid_path","存储目录必须是服务端可访问的绝对路径。");
        var full=Path.GetFullPath(root);
        if(full.TrimEnd(Path.DirectorySeparatorChar)==Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar)) throw new BusinessException(400,"invalid_path","不能使用磁盘根目录。");
        for(var d=new DirectoryInfo(full);d is not null;d=d.Parent)
            if(d.Exists && (d.Attributes&FileAttributes.ReparsePoint)!=0) throw new BusinessException(400,"invalid_path","存储路径不能包含链接或重解析目录。");
    }
    public static string FilePath(StorageLocation location,string key)
    {
        ValidateRoot(location.RootPath);
        var root=Path.GetFullPath(location.RootPath).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var full=Path.GetFullPath(Path.Combine(root,key));
        if(!full.StartsWith(root,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)) throw new BusinessException(400,"invalid_path","非法文件路径。");
        for(var d=Directory.GetParent(full);d is not null && d.FullName.Length>=root.TrimEnd(Path.DirectorySeparatorChar).Length;d=d.Parent)
            if(d.Exists && (d.Attributes&FileAttributes.ReparsePoint)!=0) throw new BusinessException(400,"invalid_path","文件路径不能包含链接。");
        if(File.Exists(full) && (File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0) throw new BusinessException(400,"invalid_path","文件不能是链接。");
        return full;
    }
    public static long? FreeBytes(string path)
    {
        try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace; } catch(IOException){return null;}
    }
    private void Space(string path,long requested)
    {
        if(FreeBytes(path) is long available && available<requested+options.MinimumFreeBytes) throw new BusinessException(507,"storage_full","目标存储空间不足，请迁移存储位置后重试。");
    }
    public async Task<FileView> UploadAsync(Actor actor,string name,string purpose,Stream stream,CancellationToken ct)
    {
        using var uploadTimeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
        uploadTimeout.CancelAfter(TimeSpan.FromMinutes(15));ct=uploadTimeout.Token;
        if(purpose is not ("exam-study" or "general")) throw new BusinessException(400,"invalid_purpose","未注册的上传用途。");
        actor.Require(purpose=="exam-study" ? "study.knowledge.manage":"system.files.manage");
        name=Path.GetFileName(name.Replace('\\','/'));
        if(string.IsNullOrWhiteSpace(name) || name.Length>250) throw new BusinessException(400,"invalid_file","文件名无效。");
        var extension=Path.GetExtension(name).ToLowerInvariant();
        if(purpose=="exam-study" && extension is not (".pdf" or ".docx")) throw new BusinessException(415,"unsupported_file","知识库支持文本 PDF 和 DOCX；旧版 DOC 请另存为 DOCX。");
        var file=await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>
        {
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var location=await db.Set<StorageLocation>().SingleOrDefaultAsync(x=>x.Writable,ct) ?? throw new BusinessException(503,"storage_unavailable","存储尚未初始化。");
            Space(location.RootPath,options.MaximumUploadBytes);
            var f=new StoredFile{OwnerId=actor.Id,Name=name,Purpose=purpose,LocationId=location.Id,ContentType=extension==".pdf"?"application/pdf":extension==".docx"?"application/vnd.openxmlformats-officedocument.wordprocessingml.document":"application/octet-stream"};
            f.ObjectKey=$"files/{DateTime.UtcNow:yyyy/MM}/{f.Id:N}.bin";db.Add(f);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return f;
        });
        var loc=await db.Set<StorageLocation>().SingleAsync(x=>x.Id==file.LocationId,ct);
        var path=FilePath(loc,file.ObjectKey); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            await using(var target=new FileStream(path+".part",FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))
            {
                var buffer=new byte[65536];int count;
                while((count=await stream.ReadAsync(buffer,ct))>0)
                {
                    file.Length+=count;
                    if(file.Length>options.MaximumUploadBytes) throw new BusinessException(413,"file_too_large",$"文件超过 {options.MaximumUploadBytes/1024/1024} MiB。");
                    await target.WriteAsync(buffer.AsMemory(0,count),ct);
                }
                await target.FlushAsync(ct);
            }
            if(file.Length==0) throw new BusinessException(400,"empty_file","不能上传空文件。");
            await using(var input=File.OpenRead(path+".part"))
            {
                var header=new byte[5];var count=await input.ReadAsync(header,ct);
                if(purpose=="exam-study" && (count<4 || (extension==".pdf" ? !header.AsSpan(0,5).SequenceEqual("%PDF-"u8) : header[0]!=0x50 || header[1]!=0x4b))) throw new BusinessException(415,"invalid_file_content","文件内容与扩展名不一致。");
                input.Position=0;file.Sha256=Convert.ToHexString(await SHA256.HashDataAsync(input,ct));
            }
            File.Move(path+".part",path);
            file.Status="ready";await db.SaveChangesAsync(ct);
            return ToView(file,0);
        }
        catch
        {
            file.Status="failed";
            try { await db.SaveChangesAsync(CancellationToken.None); if(File.Exists(path+".part")) File.Delete(path+".part"); } catch(IOException) { }
            throw;
        }
    }
    public static FileView ToView(StoredFile f,int refs)=>new(f.Id,f.Name,f.ContentType,f.Length,f.Sha256,f.Purpose,f.OwnerId,f.Status,refs,f.CreatedAtUtc);
    public async Task<IReadOnlyList<FileView>> ListAsync(Actor actor,CancellationToken ct)
    {
        actor.Require("system.files.manage");
        var refs=await db.Set<FileReference>().GroupBy(x=>x.FileId).Select(x=>new{Id=x.Key,Count=x.Count()}).ToDictionaryAsync(x=>x.Id,x=>x.Count,ct);
        return (await db.Set<StoredFile>().Where(x=>actor.Administrator || x.OwnerId==actor.Id).OrderByDescending(x=>x.CreatedAtUtc).ToListAsync(ct)).Select(x=>ToView(x,refs.GetValueOrDefault(x.Id))).ToArray();
    }
    public async Task<OpenedFile> OpenAsync(Actor actor,Guid id,CancellationToken ct)
    {
        var file=await db.Set<StoredFile>().SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new BusinessException(404,"not_found","文件不存在。");
        if(!actor.Administrator && actor.Id!=file.OwnerId && !await db.Set<FileReference>().AnyAsync(x=>x.FileId==id&&x.OwnerId==actor.Id,ct))actor.Own(file.OwnerId);
        actor.Require(file.Purpose=="exam-study"?"exam-study.access":"system.files.manage");
        return await OpenInternalAsync(id,ct);
    }
    public async Task<OpenedFile> OpenInternalAsync(Guid id,CancellationToken ct)
    {
        var f=await db.Set<StoredFile>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new BusinessException(404,"not_found","文件不存在。");
        if(f.Status!="ready") throw new BusinessException(409,"file_unavailable","文件尚未就绪或已清理。");
        var loc=await db.Set<StorageLocation>().AsNoTracking().SingleAsync(x=>x.Id==f.LocationId,ct);
        try { return new OpenedFile(new FileStream(FilePath(loc,f.ObjectKey),FileMode.Open,FileAccess.Read,FileShare.Read,65536,true),f.Name,f.ContentType); }
        catch(IOException) { throw new BusinessException(503,"file_unavailable","存储文件暂时不可读取，请检查存储位置。"); }
    }
    public async Task<IReadOnlyList<FileReference>> ReferencesAsync(Actor actor,Guid id,CancellationToken ct)
    {
        actor.Require("system.files.manage");
        var file=await db.Set<StoredFile>().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new BusinessException(404,"not_found","文件不存在。");
        actor.Own(file.OwnerId);return await db.Set<FileReference>().Where(x=>x.FileId==id).ToListAsync(ct);
    }
    public async Task LinkAsync(Actor actor,Guid id,Guid entityId,Guid referenceOwnerId,string module,CancellationToken ct)
    {
        var f=await db.Set<StoredFile>().SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new BusinessException(404,"not_found","文件不存在。");
        actor.Own(f.OwnerId); actor.Own(referenceOwnerId); actor.Require(module=="exam-study"?"exam-study.access":"system.files.manage");
        if(f.Status!="ready" || f.Purpose!=module) throw new BusinessException(409,"invalid_file","文件未就绪或用途不匹配。");
        if(!await db.Set<FileReference>().AnyAsync(x=>x.FileId==id && x.EntityId==entityId,ct)) db.Add(new FileReference{FileId=id,EntityId=entityId,OwnerId=referenceOwnerId,Module=module});
        // The caller's source transaction commits the reference and source together.
    }
    public async Task UnlinkAsync(Guid entityId,CancellationToken ct)
    {
        foreach(var r in await db.Set<FileReference>().Where(x=>x.EntityId==entityId).ToListAsync(ct)) r.IsDeleted=true;
    }
    public async Task PurgeAsync(Actor actor,Guid id,CancellationToken ct)
    {
        if(!actor.Administrator) throw new BusinessException(403,"forbidden","只有管理员可以永久删除文件。");
        var f=await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>
        {
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var row=await db.Set<StoredFile>().SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new BusinessException(404,"not_found","文件不存在。");
            if(await db.Set<FileReference>().AnyAsync(x=>x.FileId==id,ct) || await db.Set<FileMigrationItem>().AnyAsync(x=>x.FileId==id && x.Status!="cleaned",ct) || await db.Set<WorkJob>().AnyAsync(x=>x.Kind=="migrate" && x.Status!="completed",ct)) throw new BusinessException(409,"file_referenced","文件仍有引用或迁移尚未清理，不能删除。");
            if(row.Status=="uploading") throw new BusinessException(409,"file_busy","文件正在上传。");
            row.Status="purging";row.Revision++;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return row;
        });
        var loc=await db.Set<StorageLocation>().SingleAsync(x=>x.Id==f.LocationId,ct);
        File.Delete(FilePath(loc,f.ObjectKey));f.Status="purged";await db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<StorageView>> LocationsAsync(Actor actor,CancellationToken ct)
    {
        actor.Require("system.storage.manage");
        var files=await db.Set<StoredFile>().Where(x=>x.Status=="ready").ToListAsync(ct);
        return (await db.Set<StorageLocation>().ToListAsync(ct)).Select(x=>new StorageView(x.Id,x.Name,x.RootPath,x.Writable,FreeBytes(x.RootPath),files.LongCount(f=>f.LocationId==x.Id),files.Where(f=>f.LocationId==x.Id).Sum(f=>f.Length))).ToArray();
    }
    public async Task<StorageLocation> AddLocationAsync(Actor actor,LocationRequest r,CancellationToken ct)
    {
        actor.Require("system.storage.manage");ValidateRoot(r.RootPath);var root=Path.GetFullPath(r.RootPath).TrimEnd(Path.DirectorySeparatorChar);
        if(string.IsNullOrWhiteSpace(r.Name)) throw new BusinessException(400,"invalid_name","请填写存储名称。");
        foreach(var existing in await db.Set<StorageLocation>().ToListAsync(ct))
        {
            var other=Path.GetFullPath(existing.RootPath).TrimEnd(Path.DirectorySeparatorChar);
            if(root.Equals(other,StringComparison.OrdinalIgnoreCase) || root.StartsWith(other+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || other.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new BusinessException(409,"overlapping_storage","存储位置不能相同或互相包含。");
        }
        Directory.CreateDirectory(root);var probe=Path.Combine(root,$".write-check-{Guid.NewGuid():N}");await File.WriteAllTextAsync(probe,"",ct);File.Delete(probe);
        var loc=new StorageLocation{Name=r.Name.Trim(),RootPath=root};db.Add(loc);await db.SaveChangesAsync(ct);return loc;
    }
    public async Task<Guid> MigrateAsync(Actor actor,MigrationRequest r,CancellationToken ct)
    {
        actor.Require("system.storage.manage");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>
        {
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            if(r.SourceLocationId==r.TargetLocationId || await db.Set<WorkJob>().AnyAsync(x=>(x.Kind=="migrate"||x.Kind=="cleanup") && x.Status!="completed",ct)) throw new BusinessException(409,"migration_busy","源和目标须不同，且须先完成现有迁移或清理任务。");
            var source=await db.Set<StorageLocation>().SingleOrDefaultAsync(x=>x.Id==r.SourceLocationId,ct) ?? throw new BusinessException(404,"not_found","源位置不存在。");
            var target=await db.Set<StorageLocation>().SingleOrDefaultAsync(x=>x.Id==r.TargetLocationId,ct) ?? throw new BusinessException(404,"not_found","目标位置不存在。");
            ValidateRoot(target.RootPath);
            var bytes=await db.Set<StoredFile>().Where(x=>x.LocationId==source.Id && x.Status=="ready").SumAsync(x=>x.Length,ct);Space(target.RootPath,bytes);
            if(source.Writable){source.Writable=false;await db.SaveChangesAsync(ct);target.Writable=true;}
            var job=new WorkJob{OwnerId=actor.Id,Kind="migrate",TargetId=target.Id,PayloadJson=JsonData.Write(r)};db.Add(job);
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return job.Id;
        });
    }
    public async Task ProcessMigrationAsync(WorkJob job,CancellationToken ct)
    {
        var r=JsonData.Read<MigrationRequest>(job.PayloadJson);
        var source=await db.Set<StorageLocation>().SingleAsync(x=>x.Id==r.SourceLocationId,ct);
        var target=await db.Set<StorageLocation>().SingleAsync(x=>x.Id==r.TargetLocationId,ct);
        var pending=await db.Set<StoredFile>().Where(x=>x.LocationId==source.Id && x.Status=="ready").ToListAsync(ct);
        foreach(var f in pending)
        {
            ct.ThrowIfCancellationRequested();Space(target.RootPath,f.Length);
            var item=await db.Set<FileMigrationItem>().SingleOrDefaultAsync(x=>x.JobId==job.Id && x.FileId==f.Id,ct);
            if(item is null){item=new FileMigrationItem{JobId=job.Id,FileId=f.Id,SourceLocationId=source.Id,TargetLocationId=target.Id};db.Add(item);await db.SaveChangesAsync(ct);}
            var from=FilePath(source,f.ObjectKey);var to=FilePath(target,f.ObjectKey);Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            await using(var input=File.OpenRead(from)) await using(var output=new FileStream(to+".partial",FileMode.Create,FileAccess.Write,FileShare.None,65536,true)) await input.CopyToAsync(output,ct);
            await VerifyAsync(to+".partial",f,ct);File.Move(to+".partial",to,true);
            // Immutable bytes plus optimistic row revision prevent a stale copy from overriding a newer location.
            f.LocationId=target.Id;f.Revision++;item.Status="copied";item.Error="";await db.SaveChangesAsync(ct);
        }
        if(await db.Set<StoredFile>().AnyAsync(x=>x.LocationId==source.Id && x.Status=="uploading",ct)) throw new BusinessException(503,"uploads_draining","等待迁移前已经开始的上传完成，再继续扫描。");
    }
    public static async Task VerifyAsync(string path,StoredFile f,CancellationToken ct)
    {
        await using var input=File.OpenRead(path);
        if(input.Length!=f.Length || !Convert.ToHexString(await SHA256.HashDataAsync(input,ct)).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase)) throw new BusinessException(409,"checksum_mismatch","文件校验失败，保留源文件并停止切换。");
    }
    public async Task CleanupAsync(Actor actor,Guid jobId,CancellationToken ct)
    {
        actor.Require("system.storage.manage");
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>
        {
            await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            if(await db.Set<WorkJob>().AnyAsync(x=>(x.Kind=="migrate"||x.Kind=="cleanup")&&x.Status!="completed",ct))throw new BusinessException(409,"storage_busy","先完成现有迁移或清理任务。");
            if(!await db.Set<WorkJob>().AnyAsync(x=>x.Id==jobId&&x.Kind=="migrate"&&x.Status=="completed",ct))throw new BusinessException(409,"migration_incomplete","迁移尚未完成。");
            db.Add(new WorkJob{OwnerId=actor.Id,Kind="cleanup",TargetId=jobId});await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
        });
    }
    public async Task ProcessCleanupAsync(Guid jobId,CancellationToken ct)
    {
        var job=await db.Set<WorkJob>().SingleOrDefaultAsync(x=>x.Id==jobId && x.Kind=="migrate",ct);
        if(job?.Status!="completed") throw new BusinessException(409,"migration_incomplete","只有完成的迁移可以清理旧副本。");
        if(await db.Set<WorkJob>().AnyAsync(x=>x.Kind=="migrate" && x.Status!="completed",ct)) throw new BusinessException(409,"migration_busy","其他迁移期间不能清理旧副本。");
        foreach(var item in await db.Set<FileMigrationItem>().Where(x=>x.JobId==jobId && x.Status=="copied").ToListAsync(ct))
        {
            var f=await db.Set<StoredFile>().SingleAsync(x=>x.Id==item.FileId,ct);
            if(f.LocationId==item.SourceLocationId) throw new BusinessException(409,"source_active","原位置又被使用，不能清理。");
            var active=await db.Set<StorageLocation>().SingleAsync(x=>x.Id==f.LocationId,ct);
            await VerifyAsync(FilePath(active,f.ObjectKey),f,ct);
            var old=await db.Set<StorageLocation>().SingleAsync(x=>x.Id==item.SourceLocationId,ct);
            try{File.Delete(FilePath(old,f.ObjectKey));item.Status="cleaned";item.Error="";}
            catch(IOException){item.Error="旧文件仍被读取，请稍后再次清理。";}
            await db.SaveChangesAsync(ct);
        }
        if(await db.Set<FileMigrationItem>().AnyAsync(x=>x.JobId==jobId&&x.Status=="copied",ct))throw new BusinessException(503,"files_in_use","部分旧副本仍在读取，稍后继续清理。");
    }
}
