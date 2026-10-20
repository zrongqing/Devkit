using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Devkit.FileStorage.Application.Abstractions;
using Devkit.FileStorage.Application.Files;

namespace Devkit.FileStorage.Infrastructure.Identity;

internal sealed class RemoteIdentitySession(IHttpClientFactory clients) : IIdentitySession
{
    public Actor? Current { get; private set; }

    public async Task AuthenticateAsync(string accessToken, Guid expectedSubject, CancellationToken cancellationToken)
    {
        // Scoped to one incoming request: filters and application services reuse this assertion.
        Current = null;
        try
        {
            using var client = clients.CreateClient("identity");
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/permissions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new FileStorageException(401, "unauthorized", "登录状态已失效，请重新登录。");
            if (!response.IsSuccessStatusCode) throw Unavailable();
            var envelope = await response.Content.ReadFromJsonAsync<IdentityEnvelope>(cancellationToken);
            var subject = envelope?.Data;
            if (subject is null || subject.Id != expectedSubject || subject.Permissions is null || subject.MenuCodes is null
                || subject.Permissions.Any(string.IsNullOrWhiteSpace) || subject.MenuCodes.Any(string.IsNullOrWhiteSpace))
                throw Unavailable();
            // Map the wire assertion to this context's own actor model.
            Current = new Actor(subject.Id, subject.Administrator, subject.Permissions, subject.MenuCodes);
        }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (JsonException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw Unavailable(); }
    }

    private static FileStorageException Unavailable() => new(503, "identity_unavailable", "身份服务暂时不可用，请稍后重试。");
    private sealed record IdentityEnvelope(IdentityAssertion? Data);
    private sealed record IdentityAssertion(Guid Id, bool Administrator, string[]? Permissions, string[]? MenuCodes);
}

internal sealed class RemoteFileAccess(IIdentitySession identity) : IFileAccess
{
    public Task<Actor> CurrentAsync(CancellationToken ct = default) => Task.FromResult(
        identity.Current ?? throw new FileStorageException(401, "unauthorized", "请先登录。"));
}
