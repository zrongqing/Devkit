using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
namespace Devkit.AspNetCore.Endpoints;

/// <summary>
/// Defines a discoverable Minimal API endpoint module.
/// </summary>
public interface IEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
