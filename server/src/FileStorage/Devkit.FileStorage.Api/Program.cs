using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Devkit.AspNetCore.Authentication;
using Devkit.AspNetCore.Hosting;
using Devkit.FileStorage.Application.Abstractions;
using Devkit.FileStorage.Application.Files;
using Devkit.FileStorage.Infrastructure;
using Devkit.FileStorage.Infrastructure.Configuration;
using Devkit.FileStorage.Infrastructure.Files;
using Devkit.FileStorage.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace Devkit.FileStorage.Api;
public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddDevkitExternalConfiguration("DEVKIT_FILESTORAGE_CONFIG_FILE", args);
        builder.Services.AddFileStorageInfrastructure(builder.Configuration);
        builder.Services.AddDevkitApi(builder.Configuration, typeof(Program).Assembly);
        builder.Services.AddOptions<FormOptions>().Configure<IOptions<FileStorageOptions>>((form, files) =>
            form.MultipartBodyLengthLimit = files.Value.MaximumUploadBytes + 1024 * 1024);
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024L * 1024 * 1024 + 1024 * 1024);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, JwtValidationKey>((options, configured, keys) =>
                options.ConfigureDevkitJwt(configured.Value.Issuer, configured.Value.Audience, keys.ValidationKey, keys.Algorithm, async context =>
                {
                    if (!Guid.TryParse(context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
                    { context.Fail("Token subject is invalid."); return; }
                    try
                    {
                        var bearer = context.Request.Headers.Authorization.ToString();
                        await context.HttpContext.RequestServices.GetRequiredService<IIdentitySession>()
                            .AuthenticateAsync(bearer["Bearer ".Length..], userId, context.HttpContext.RequestAborted);
                    }
                    catch (FileStorageException exception) when (exception.Status == 401) { context.Fail("Identity session is no longer valid."); }
                }));
        builder.Services.AddAuthorization();
        var app = builder.Build();
        _ = app.Services.GetRequiredService<JwtValidationKey>();
        app.UseDevkitApi();
        app.MapDevkitApi("Devkit File Storage API");
        app.Run();
    }
}
