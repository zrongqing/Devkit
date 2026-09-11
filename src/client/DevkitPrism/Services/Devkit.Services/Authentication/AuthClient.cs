using System.Net.Http.Headers;
using System.Net.Http.Json;
using Devkit.Services.Interfaces.Authentication;

namespace Devkit.Services.Authentication;

public sealed class AuthClient(HttpClient httpClient) : IAuthClient
{
    public Task<UserProfileDto> RegisterAsync(
        string userName,
        string email,
        string password,
        CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileDto>(HttpMethod.Post, "api/v1/auth/register", new { userName, email, password }, null, cancellationToken);

    public Task<TokenPairDto> LoginAsync(string account, string password, CancellationToken cancellationToken = default) =>
        SendAsync<TokenPairDto>(HttpMethod.Post, "api/v1/auth/login", new { account, password }, null, cancellationToken);

    public Task<TokenPairDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        SendAsync<TokenPairDto>(HttpMethod.Post, "api/v1/auth/refresh", new { refreshToken }, null, cancellationToken);

    public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        SendWithoutResponseAsync(HttpMethod.Post, "api/v1/auth/logout", new { refreshToken }, null, cancellationToken);

    public Task LogoutAllAsync(string accessToken, CancellationToken cancellationToken = default) =>
        SendWithoutResponseAsync(HttpMethod.Post, "api/v1/auth/logout-all", null, accessToken, cancellationToken);

    public Task<UserProfileDto> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileDto>(HttpMethod.Get, "api/v1/auth/me", null, accessToken, cancellationToken);

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, body, accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken);
        return envelope is null ? throw new InvalidOperationException("The server returned an empty authentication response.") : envelope.Data;
    }

    private async Task SendWithoutResponseAsync(
        HttpMethod method,
        string path,
        object? body,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, body, accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body, string? accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return request;
    }

    private sealed record ApiResponse<T>(T Data, string TraceId);
}
