using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Devkit.AspNetCore.Authentication;

public static class JwtBearerOptionsExtensions
{
    public static void ConfigureDevkitJwt(this JwtBearerOptions options, string issuer, string audience,
        SecurityKey verificationKey, string algorithm, Func<TokenValidatedContext, Task> validateSession)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = issuer,
            ValidateAudience = true, ValidAudience = audience,
            ValidateIssuerSigningKey = true, IssuerSigningKey = verificationKey,
            ValidAlgorithms = [algorithm], ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = validateSession,
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized, Title = "Authentication is required.",
                    Extensions = { ["code"] = "unauthorized", ["traceId"] = context.HttpContext.TraceIdentifier }
                });
            }
        };
    }
}
