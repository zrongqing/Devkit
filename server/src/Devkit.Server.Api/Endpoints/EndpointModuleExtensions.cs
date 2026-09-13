using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devkit.Server.Api.Endpoints;

public static class EndpointModuleExtensions
{
    public static IServiceCollection AddEndpointModules(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        var moduleTypes = assemblies
            .Distinct()
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
                && type.IsAssignableTo(typeof(IEndpointModule)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var moduleType in moduleTypes)
        {
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton(typeof(IEndpointModule), moduleType.AsType()));
        }

        return services;
    }

    public static IEndpointRouteBuilder MapEndpointModules(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        foreach (var module in endpoints.ServiceProvider.GetRequiredService<IEnumerable<IEndpointModule>>())
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
