using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Devkit.Server.Infrastructure.Health;

public sealed class RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await connectionMultiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy("Redis is reachable", new Dictionary<string, object> { ["latencyMs"] = latency.TotalMilliseconds });
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis health check failed", exception);
        }
    }
}
