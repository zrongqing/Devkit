using Devkit.AspNetCore.Authentication;
using Devkit.AspNetCore.Hosting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Devkit.Identity.Application.Abstractions;
using Devkit.Identity.Infrastructure;
using Devkit.Identity.Infrastructure.Configuration;
using Devkit.Identity.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Devkit.Identity.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddDevkitExternalConfiguration("DEVKIT_IDENTITY_CONFIG_FILE", args);
        builder.Services.AddIdentityInfrastructure(builder.Configuration);
        builder.Services.AddDevkitApi(builder.Configuration, typeof(Program).Assembly);

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, JwtKeyMaterial>((options, configuredJwtOptions, keys) =>
            {
                var jwtOptions = configuredJwtOptions.Value;
                options.ConfigureDevkitJwt(jwtOptions.Issuer, jwtOptions.Audience,
                    keys.ValidationKey, keys.Algorithm, async context =>
                {
                    var subject = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    var versionClaim = context.Principal?.FindFirstValue("auth_version");
                    if (!Guid.TryParse(subject, out var userId) || !int.TryParse(versionClaim, out var authenticationVersion))
                    {
                        context.Fail("Token claims are invalid.");
                        return;
                    }
    
                    var validator = context.HttpContext.RequestServices.GetRequiredService<IAccessTokenValidator>();
                    if (!await validator.IsCurrentAsync(userId, authenticationVersion, context.HttpContext.RequestAborted))
                    {
                        context.Fail("Token has been invalidated.");
                    }
                });
            });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many authentication requests.",
                    Extensions =
                    {
                        ["code"] = "rate_limited",
                        ["traceId"] = context.HttpContext.TraceIdentifier
                    }
                }, cancellationToken);
            };
        });
        
        var app = builder.Build();
        // Fail at startup for missing or unsuitable key files, before accepting any traffic.
        _ = app.Services.GetRequiredService<JwtKeyMaterial>();
        app.UseDevkitApi(beforeAuthentication: application => application.UseRateLimiter());
        app.MapDevkitApi("Devkit Identity API");
        app.Run();
    }
}
