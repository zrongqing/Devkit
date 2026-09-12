using System.Security.Cryptography;
using System.Text;
using Devkit.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Devkit.Server.Api.Endpoints;

public sealed class ModuleApiKeyEndpointFilter(IOptions<ModuleControlOptions> configuredOptions) : IEndpointFilter
{
    public const string HeaderName = "X-Devkit-Module-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var options = configuredOptions.Value;
        if (!options.Enabled)
        {
            return Problem(
                context.HttpContext,
                StatusCodes.Status503ServiceUnavailable,
                "module_control_disabled",
                "Module control is disabled on this server.");
        }

        var suppliedKey = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (!Matches(suppliedKey, options.ApiKey))
        {
            return Problem(
                context.HttpContext,
                StatusCodes.Status401Unauthorized,
                "invalid_module_credential",
                "A valid module credential is required.");
        }

        return await next(context);
    }

    private static bool Matches(string suppliedKey, string configuredKey)
    {
        if (string.IsNullOrEmpty(suppliedKey) || string.IsNullOrEmpty(configuredKey))
        {
            return false;
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedKey));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
        return CryptographicOperations.FixedTimeEquals(suppliedHash, configuredHash);
    }

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = context.TraceIdentifier
            });
}
