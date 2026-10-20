using Devkit.Commons.Serialization;
using System.Text.Json;
using Devkit.FileStorage.Application.Files;
using Devkit.FileStorage.Domain.Files;
using Devkit.FileStorage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Devkit.FileStorage.Infrastructure.Files;

public sealed class FileStorageWorker(IServiceScopeFactory scopes,ILogger<FileStorageWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<LocalFileService>().EnsureDefaultAsync(stoppingToken);
                break;
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception e)
            {
                logger.LogWarning("File storage initialization pending: {Type}",e.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(2),stoppingToken);
            }
        }
        while(!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception e){logger.LogWarning("File storage worker cycle failed: {Type}",e.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(2),stoppingToken);
        }
    }
    private async Task TickAsync(CancellationToken ct)
    {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<FileStorageDbContext>();var now=DateTime.UtcNow;
        // Recover uploads whose process terminated. Completed originals remain on disk for administrator recovery.
        await db.Set<StoredFile>().Where(x=>x.Status=="uploading"&&x.CreatedAtUtc<now.AddHours(-2)).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"failed"),ct);
        var job=await db.Set<StorageJob>().AsNoTracking().Where(x=>(x.Status=="queued"&&(!x.RetryAfterUtc.HasValue||x.RetryAfterUtc<=now)) || (x.Status=="running"&&x.LeaseUntilUtc<now)).OrderBy(x=>x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if(job is null)return;
        var lease=Guid.NewGuid();
        var claimed=await db.Set<StorageJob>().Where(x=>x.Id==job.Id&&((x.Status=="queued"&&(!x.RetryAfterUtc.HasValue||x.RetryAfterUtc<=now))||(x.Status=="running"&&x.LeaseUntilUtc<now))).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"running").SetProperty(x=>x.LeaseId,lease).SetProperty(x=>x.LeaseUntilUtc,now.AddMinutes(2)).SetProperty(x=>x.Attempts,x=>x.Attempts+1).SetProperty(x=>x.Error,""),ct);
        if(claimed!=1)return;
        using var workCancellation=CancellationTokenSource.CreateLinkedTokenSource(ct);
        var renewal=RenewAsync(job.Id,lease,workCancellation);
        try
        {
            var handler=scope.ServiceProvider.GetRequiredService<StorageJobHandler>();
            await handler.RunAsync(job,workCancellation.Token);
            await db.Set<StorageJob>().Where(x=>x.Id==job.Id&&x.LeaseId==lease).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"completed").SetProperty(x=>x.Progress,100).SetProperty(x=>x.LeaseId,(Guid?)null).SetProperty(x=>x.LeaseUntilUtc,(DateTime?)null),ct);
        }
        catch(Exception e) when(!ct.IsCancellationRequested)
        {
            var code=e is FileStorageException business?business.Code:"processing_failed";
            var message=e is FileStorageException b?b.Message:"存储任务未完成，请检查存储位置、访问权限和剩余空间后重试。";
            var status=job.Attempts>=2?"failed":"queued";
            using var errorScope=scopes.CreateScope();var errorDb=errorScope.ServiceProvider.GetRequiredService<FileStorageDbContext>();
            await errorDb.Set<StorageJob>().Where(x=>x.Id==job.Id&&x.LeaseId==lease).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,status).SetProperty(x=>x.Error,message).SetProperty(x=>x.RetryAfterUtc,DateTime.UtcNow.AddSeconds(20)).SetProperty(x=>x.LeaseUntilUtc,(DateTime?)null).SetProperty(x=>x.LeaseId,(Guid?)null),ct);
            logger.LogWarning("Storage job {JobId} ({Kind}) failed: {Code} / {Type}",job.Id,job.Kind,code,e.GetType().Name);
        }
        finally {workCancellation.Cancel();try{await renewal;}catch(OperationCanceledException){}}
    }
    private async Task RenewAsync(Guid jobId,Guid lease,CancellationTokenSource cancellation)
    {
        try
        {
        while(!cancellation.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(20),cancellation.Token);
            using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<FileStorageDbContext>();
            var affected=await db.Set<StorageJob>().Where(x=>x.Id==jobId&&x.LeaseId==lease&&x.Status=="running").ExecuteUpdateAsync(s=>s.SetProperty(x=>x.LeaseUntilUtc,DateTime.UtcNow.AddMinutes(2)),cancellation.Token);
            if(affected!=1){cancellation.Cancel();return;}
        }
        }
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { }
        catch(Exception e)
        {
            logger.LogWarning("Storage lease renewal failed: {Type}",e.GetType().Name);
            cancellation.Cancel();
        }
    }
}

public sealed class StorageJobHandler(LocalFileService files)
{
    public Task RunAsync(StorageJob job,CancellationToken ct) => job.Kind switch
    {
        "migrate" => files.ProcessMigrationAsync(job,ct),
        "cleanup" => files.ProcessCleanupAsync(job.TargetId,ct),
        _ => throw new FileStorageException(400,"invalid_job","未知存储任务类型。")
    };
}
