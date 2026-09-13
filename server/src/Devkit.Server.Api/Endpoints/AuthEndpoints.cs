using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Devkit.Server.Api.Contracts;
using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts.Auth;

namespace Devkit.Server.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth").WithTags("Authentication");
        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Registers a user when public registration is enabled")
            .RequireRateLimiting("authentication")
            .Produces<ApiResponse<UserProfileDto>>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Authenticates by user name or email")
            .RequireRateLimiting("authentication")
            .Produces<ApiResponse<TokenPairDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked);
        group.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshToken")
            .WithSummary("Rotates a refresh token and issues a new token pair")
            .Produces<ApiResponse<TokenPairDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("Revokes one refresh-token session")
            .Produces(StatusCodes.Status204NoContent);
        group.MapPost("/logout-all", LogoutAllAsync)
            .WithName("LogoutAll")
            .WithSummary("Revokes all sessions and invalidates existing access tokens")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        group.MapGet("/me", GetMeAsync)
            .WithName("GetCurrentUser")
            .WithSummary("Returns the current user, using Redis with a SQL Server fallback")
            .RequireAuthorization()
            .Produces<ApiResponse<UserProfileDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
        return group;
    }

    // [EndpointSummary("Registers a user when public registration is enabled")]
    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        HttpContext context,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request.UserName, request.Email, request.Password, cancellationToken);
        if (!result.Succeeded)
        {
            return ToProblem(result.ErrorCode!, result.ErrorMessage!, context);
        }

        return Results.Created("/api/v1/auth/me", new ApiResponse<UserProfileDto>(result.Value!, context.TraceIdentifier));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext context,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Account, request.Password, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new ApiResponse<TokenPairDto>(result.Value!, context.TraceIdentifier))
            : ToProblem(result.ErrorCode!, result.ErrorMessage!, context);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        HttpContext context,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new ApiResponse<TokenPairDto>(result.Value!, context.TraceIdentifier))
            : ToProblem(result.ErrorCode!, result.ErrorMessage!, context);
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAllAsync(
        ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (TryGetUserId(principal, out var userId))
        {
            await authService.LogoutAllAsync(userId, cancellationToken);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> GetMeAsync(
        ClaimsPrincipal principal,
        HttpContext context,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return Problem("invalid_access_token", "The access token subject is invalid.", StatusCodes.Status401Unauthorized, context);
        }

        var result = await authService.GetProfileAsync(userId, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new ApiResponse<UserProfileDto>(result.Value!, context.TraceIdentifier))
            : ToProblem(result.ErrorCode!, result.ErrorMessage!, context);
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    private static IResult ToProblem(string code, string message, HttpContext context)
    {
        var status = code switch
        {
            "registration_disabled" => StatusCodes.Status403Forbidden,
            "account_exists" => StatusCodes.Status409Conflict,
            "invalid_credentials" or "invalid_refresh_token" or "refresh_token_reused" => StatusCodes.Status401Unauthorized,
            "account_locked" => StatusCodes.Status423Locked,
            "user_not_found" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };
        return Problem(code, message, status, context);
    }

    private static IResult Problem(string code, string message, int status, HttpContext context) =>
        Results.Problem(
            statusCode: status,
            title: message,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = context.TraceIdentifier
            });
}
