using Devkit.Server.Application.Abstractions;
using Devkit.Server.Infrastructure.SystemInfo;
using Microsoft.Extensions.DependencyInjection;

namespace Devkit.Server.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDevkitInfrastructure(this IServiceCollection services, ServerRuntimeOptions options)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(options);
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        return services;
    }
}
