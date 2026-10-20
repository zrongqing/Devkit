using Devkit.Identity.Application.Abstractions;
using Devkit.Identity.Infrastructure.Caching;
using Devkit.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Identity.Infrastructure.Identity;

internal sealed class AccessTokenValidator(IdentityDbContext dbContext, UserProfileCache cache) : IAccessTokenValidator
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
