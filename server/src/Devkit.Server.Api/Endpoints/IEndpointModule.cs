namespace Devkit.Server.Api.Endpoints;

/// <summary>
/// Defines a discoverable Minimal API endpoint module.
/// </summary>
public interface IEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
