using Devkit.Application.Application.Abstractions;
using Devkit.Application.Infrastructure.Caching;
using Devkit.Application.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Application.Infrastructure.Identity;

internal sealed class AccessTokenValidator(ApplicationDbContext dbContext, UserProfileCache cache) : IAccessTokenValidator
{
    public async Task<bool> IsCurrentAsync(Guid userId, int authenticationVersion, CancellationToken cancellationToken)
    {
        var cachedVersion = await cache.GetAuthenticationVersionAsync(userId, cancellationToken);
        if (cachedVersion.HasValue)
            return cachedVersion.Value == authenticationVersion;

        var currentVersion = await dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => (int?)user.AuthenticationVersion)
            .SingleOrDefaultAsync(cancellationToken);
        if (!currentVersion.HasValue)
            return false;

        await cache.SetAuthenticationVersionAsync(userId, currentVersion.Value, cancellationToken);
        return currentVersion.Value == authenticationVersion;
    }
}
