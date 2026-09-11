using System.Text.Json;
using Devkit.Server.Application.Contracts.Auth;
using Devkit.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Devkit.Server.Infrastructure.Caching;

internal sealed class UserProfileCache(
    IDistributedCache cache,
    IOptions<RedisOptions> options,
    ILogger<UserProfileCache> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeSpan _profileTtl = TimeSpan.FromMinutes(options.Value.ProfileTtlMinutes);

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var value = await cache.GetStringAsync(ProfileKey(userId), cancellationToken);
            return value is null ? null : JsonSerializer.Deserialize<UserProfileDto>(value, SerializerOptions);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Redis profile read failed for user {UserId}; falling back to SQL Server", userId);
            return null;
        }
    }

    public async Task SetProfileAsync(UserProfileDto profile, CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetStringAsync(
                ProfileKey(profile.Id),
                JsonSerializer.Serialize(profile, SerializerOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _profileTtl },
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Redis profile write failed for user {UserId}; request will continue", profile.Id);
        }
    }

    public async Task<int?> GetAuthenticationVersionAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var value = await cache.GetStringAsync(AuthenticationVersionKey(userId), cancellationToken);
            return int.TryParse(value, out var version) ? version : null;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Redis authentication-version read failed for user {UserId}; falling back to SQL Server", userId);
            return null;
        }
    }

    public async Task SetAuthenticationVersionAsync(Guid userId, int version, CancellationToken cancellationToken)
    {
        try
        {
            await cache.SetStringAsync(
                AuthenticationVersionKey(userId),
                version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _profileTtl },
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Redis authentication-version write failed for user {UserId}; request will continue", userId);
        }
    }

    public async Task RemoveAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(
                cache.RemoveAsync(ProfileKey(userId), cancellationToken),
                cache.RemoveAsync(AuthenticationVersionKey(userId), cancellationToken));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Redis invalidation failed for user {UserId}; request will continue", userId);
        }
    }

    private static string ProfileKey(Guid userId) => $"auth:user:{userId:N}:profile";
    private static string AuthenticationVersionKey(Guid userId) => $"auth:user:{userId:N}:version";
}
