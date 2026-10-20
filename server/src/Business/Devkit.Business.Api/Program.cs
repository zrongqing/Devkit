using Devkit.AspNetCore.Authentication;
using Devkit.AspNetCore.Hosting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Devkit.Business.Application.Abstractions;
using Devkit.Business.Infrastructure;
using Devkit.Business.Infrastructure.Workspace;
using Devkit.Business.Infrastructure.Configuration;
using Devkit.Business.Infrastructure.Identity;
using Devkit.Business.Infrastructure.SystemInfo;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Devkit.Business.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddDevkitExternalConfiguration("DEVKIT_BUSINESS_CONFIG_FILE", args);
        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        builder.Services.AddBusinessInfrastructure(builder.Configuration,
            new ServerRuntimeOptions("Devkit Business", version, builder.Environment.EnvironmentName));
        builder.Services.AddWorkspace(builder.Configuration);
        builder.Services.AddControllers();
        builder.Services.AddDevkitApi(builder.Configuration, typeof(Program).Assembly);

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, JwtValidationKey>((options, configuredJwtOptions, keys) =>
            {
                var jwtOptions = configuredJwtOptions.Value;
                options.ConfigureDevkitJwt(jwtOptions.Issuer, jwtOptions.Audience,
                    keys.ValidationKey, keys.Algorithm, async context =>
                {
                    var subject = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    if (!Guid.TryParse(subject, out var userId))
                    {
                        context.Fail("Token subject is invalid.");
                        return;
                    }
                    var bearer = context.Request.Headers.Authorization.ToString();
                    try
                    {
                        await context.HttpContext.RequestServices.GetRequiredService<IIdentitySession>()
                            .AuthenticateAsync(bearer["Bearer ".Length..], userId, context.HttpContext.RequestAborted);
                    }
                    catch (Devkit.Business.Application.Workspace.BusinessException exception) when (exception.Status == 401)
                    {
                        context.Fail("Identity session is no longer valid.");
                    }
                });
            });
        builder.Services.AddAuthorization();
        var app = builder.Build();
        // Fail at startup for missing or unsuitable key files, before accepting any traffic.
        _ = app.Services.GetRequiredService<JwtValidationKey>();
        app.UseDevkitApi();
        app.MapDevkitApi("Devkit Business API");
        app.MapControllers();
        app.Run();
    }
}
