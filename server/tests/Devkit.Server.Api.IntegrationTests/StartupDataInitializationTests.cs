using Devkit.DomainCommons.Initialization;
using Devkit.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class StartupDataInitializationTests
{
    [Fact]
    public async Task Module_can_fill_missing_rows_and_preserve_existing_values_across_starts()
    {
        var connectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
        await using var database = new SqliteConnection(connectionString);
        await database.OpenAsync();
        using var command = database.CreateCommand();
        command.CommandText = "CREATE TABLE settings (name TEXT PRIMARY KEY, value TEXT);";
        await command.ExecuteNonQueryAsync();

        await StartModuleAsync();
        command.CommandText = "UPDATE settings SET value = 'custom' WHERE name = 'theme';";
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
        await StartModuleAsync();
        command.CommandText = "SELECT value FROM settings WHERE name = 'theme';";
        Assert.Equal("custom", await command.ExecuteScalarAsync());

        async Task StartModuleAsync()
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddScoped(_ => new SqliteConnection(connectionString));
            builder.Services.AddStartupDataInitializer<SettingsInitializer>();
            using var host = builder.Build();
            await host.StartAsync();
            var worker = Assert.Single(host.Services.GetServices<IHostedService>().OfType<BackgroundService>());
            await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
            await host.StopAsync();
        }
    }

    private sealed class SettingsInitializer(SqliteConnection database) : IStartupDataInitializer
    {
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await database.OpenAsync(cancellationToken);
            using var command = database.CreateCommand();
            command.CommandText = "INSERT INTO settings VALUES ('theme', 'default') ON CONFLICT(name) DO NOTHING;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task Initializer_failure_reaches_the_host_background_exception_policy()
    {
        var builder = Host.CreateApplicationBuilder();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        builder.Services.AddSingleton(release);
        builder.Services.AddStartupDataInitializer<FailingInitializer>();
        using var host = builder.Build();
        await host.StartAsync();
        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = host.Services.GetRequiredService<IHostApplicationLifetime>()
            .ApplicationStopping.Register(() => stopping.TrySetResult());
        release.SetResult();
        await stopping.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await host.StopAsync();
    }

    private sealed class FailingInitializer(TaskCompletionSource release) : IStartupDataInitializer
    {
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await release.Task.WaitAsync(cancellationToken);
            throw new InvalidOperationException("Simulated module failure");
        }
    }

    [Fact]
    public async Task Host_runs_each_registered_initializer_once_and_disposes_scopes_on_shutdown()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<Probe>();
        builder.Services.AddScoped<ScopeMarker>();
        builder.Services.AddStartupDataInitializer<WaitingInitializer>();
        builder.Services.AddStartupDataInitializer<WaitingInitializer>();
        builder.Services.AddStartupDataInitializer<OtherInitializer>();
        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var probe = host.Services.GetRequiredService<Probe>();

        await host.StartAsync(timeout.Token);
        try
        {
            var waiting = await probe.Waiting.Task.WaitAsync(timeout.Token);
            var other = await probe.Other.Task.WaitAsync(timeout.Token);
            Assert.NotSame(waiting, other);
            Assert.False(waiting.Disposed);
        }
        finally
        {
            await host.StopAsync(timeout.Token);
        }

        Assert.Equal(1, probe.WaitingCalls);
        Assert.True((await probe.Waiting.Task).Disposed);
        Assert.True((await probe.Other.Task).Disposed);
    }

    private sealed class Probe
    {
        public TaskCompletionSource<ScopeMarker> Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ScopeMarker> Other { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int WaitingCalls;
    }

    private sealed class ScopeMarker : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class WaitingInitializer(Probe probe, ScopeMarker marker) : IStartupDataInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref probe.WaitingCalls);
            probe.Waiting.TrySetResult(marker);
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class OtherInitializer(Probe probe, ScopeMarker marker) : IStartupDataInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            probe.Other.SetResult(marker);
            return Task.CompletedTask;
        }
    }
}
