using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Devkit.Business.Infrastructure.Health;

public sealed class IdentityServiceHealthCheck(IHttpClientFactory clients) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = clients.CreateClient("identity");
            using var response = await client.GetAsync("health/ready", cancellationToken);
            return response.IsSuccessStatusCode ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Identity is not ready");
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Identity is unreachable", exception);
        }
    }
}
