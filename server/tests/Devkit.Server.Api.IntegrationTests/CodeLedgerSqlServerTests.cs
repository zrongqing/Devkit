using Devkit.Business.Application.CodeSystem;
using System.Diagnostics;
using Devkit.Business.Domain.CodeSystem;
using Devkit.Business.Infrastructure.CodeSystem;
using Devkit.Business.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class LocalDbFactAttribute : FactAttribute
{
    public LocalDbFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "SQL Server LocalDB integration requires Windows.";
    }
}

public sealed class CodeLedgerSqlServerTests
{
    [LocalDbFact]
    public async Task Real_migration_concurrent_first_period_publish_replay_and_atomic_rollback()
    {
        var instance = $"DevkitCodeTest_{Guid.NewGuid():N}";
        await LocalDb("create", instance, "-s");
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\" + instance,
            InitialCatalog = "CodeLedgerTests",
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            MaxPoolSize = 30
        };
        var services = new ServiceCollection();
        var fault = new FailAfterWrite();
        services.AddDbContext<BusinessDbContext>(options => options.UseSqlServer(connection.ConnectionString, o => o.EnableRetryOnFailure()).AddInterceptors(fault));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BusinessDbContext>();
        var ledger = new CodeLedger(provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
        var actor = Guid.NewGuid();
        const string key = "wms.receipt";
        var scenario = new CodeScenario(key, "wms", "入库单");
        try
        {
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var rule = await ledger.CreateAsync(scenario, "入库", CodeSystemTests.Simple(4), actor, false, default);
            await ledger.ChangeAsync(rule.Id, ["wms"], 1, "publish", null, "首发", actor, default);
            Task<CodeIssue> Issue(Guid req, Guid obj) => ledger.IssueAsync(key, "wms", req, obj, actor, default);
            var issued = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Issue(Guid.NewGuid(), Guid.NewGuid())));
            Assert.Equal(Enumerable.Range(1, 100).Select(x => (long)x), issued.Select(x => x.SequenceValue).Order());
            Assert.Equal(100, issued.Select(x => x.Code).Distinct().Count());
            var (request, obj) = (Guid.NewGuid(), Guid.NewGuid());
            var replays = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Issue(request, obj)));
            Assert.Single(replays.Select(x => x.Id).Distinct());
            await ledger.ChangeAsync(rule.Id, ["wms"], 2, "save_draft", CodeSystemTests.Simple(5), null, actor, default);
            var release = ledger.ChangeAsync(rule.Id, ["wms"], 3, "publish", null, "扩位", actor, default);
            var concurrent = Issue(Guid.NewGuid(), Guid.NewGuid());
            await Task.WhenAll(release, concurrent);
            var concurrentIssue = await concurrent;
            Assert.Equal(concurrentIssue.VersionNo == 1 ? "0102" : "00102", concurrentIssue.Code);
            fault.Armed = true;
            var req = Guid.NewGuid(); var id = Guid.NewGuid();
            await Assert.ThrowsAsync<InjectedFailure>(() => Issue(req, id));
            Assert.Equal(102, (await db.Set<CodeCounter>().AsNoTracking().SingleAsync()).LastValue);
            Assert.False(await db.Set<CodeIssue>().AnyAsync(x => x.RequestId == req));
            Assert.Equal("00103", (await Issue(req, id)).Code);
            Assert.Equal(103, await db.Set<CodeIssue>().CountAsync());
        }
        finally
        {
            try { await db.Database.EnsureDeletedAsync(); }
            finally
            {
                await LocalDb("stop", instance, "-k");
                await LocalDb("delete", instance);
            }
        }
    }

    private static async Task LocalDb(params string[] args)
    {
        var info = new ProcessStartInfo("SqlLocalDB.exe") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("LocalDB 未能启动。");
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
    }

    private sealed class InjectedFailure : Exception;
    private sealed class FailAfterWrite : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        {
            if (!Armed) return ValueTask.FromResult(result);
            Armed = false;
            throw new InjectedFailure();
        }
    }
}
