using Devkit.Identity.Application.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Devkit.Identity.Infrastructure.Navigation;

internal sealed class WebMenuBootstrapHostedService(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<WebMenuService>().SnapshotAsync(ct);
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
