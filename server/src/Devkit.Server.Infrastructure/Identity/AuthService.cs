using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts.Auth;
using Devkit.Server.Domain.Identity;
using Devkit.Server.Infrastructure.Caching;
using Devkit.Server.Infrastructure.Configuration;
using Devkit.Server.Infrastructure.Persistence;
using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Devkit.Server.Infrastructure.Identity;

internal sealed class AuthService(
    DevkitDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IMapper mapper,
    UserProfileCache cache,
    IOptions<JwtOptions> jwtOptions,
    IOptions<RegistrationOptions> registrationOptions,
    TimeProvider timeProvider) : IAuthService, IAccessTokenValidator
{
    private const int MaximumFailedAccessAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<AuthResult<UserProfileDto>> RegisterAsync(
        string userName,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        if (!registrationOptions.Value.Enabled)
        {
            return AuthResult<UserProfileDto>.Failure("registration_disabled", "Public registration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            return AuthResult<UserProfileDto>.Failure("invalid_user_name", "User name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return AuthResult<UserProfileDto>.Failure("invalid_email", "Email address is required.");
        }

        if (string.IsNullOrEmpty(password))
        {
            return AuthResult<UserProfileDto>.Failure("weak_password", "Password is required.");
        }

        userName = userName.Trim();
        email = email.Trim();
        if (userName.Length is < 3 or > 100)
        {
            return AuthResult<UserProfileDto>.Failure("invalid_user_name", "User name must contain between 3 and 100 characters.");
        }

        if (!IsValidEmail(email))
        {
            return AuthResult<UserProfileDto>.Failure("invalid_email", "Email address is invalid.");
        }

        var passwordError = PasswordPolicy.Validate(password);
        if (passwordError is not null)
        {
            return AuthResult<UserProfileDto>.Failure("weak_password", passwordError);
        }

        var normalizedUserName = Normalize(userName);
        var normalizedEmail = Normalize(email);
        var exists = await dbContext.Users.AnyAsync(
            user => user.NormalizedUserName == normalizedUserName || user.NormalizedEmail == normalizedEmail,
            cancellationToken);
        if (exists)
        {
            return AuthResult<UserProfileDto>.Failure("account_exists", "The user name or email is already registered.");
        }

        var role = await dbContext.Roles.SingleAsync(item => item.NormalizedName == "USER", cancellationToken);
        var user = new User
        {
            UserName = userName,
            NormalizedUserName = normalizedUserName,
            Email = email,
            NormalizedEmail = normalizedEmail
        };
        user.CreatedBy = user.Id;
        user.UpdatedBy = user.Id;
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role, CreatedBy = user.Id, UpdatedBy = user.Id });

        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return AuthResult<UserProfileDto>.Failure("account_exists", "The user name or email is already registered.");
        }

        var profile = mapper.Map<UserProfileDto>(user);
        await cache.SetProfileAsync(profile, cancellationToken);
        await cache.SetAuthenticationVersionAsync(user.Id, user.AuthenticationVersion, cancellationToken);
        return AuthResult<UserProfileDto>.Success(profile);
    }

    public async Task<AuthResult<TokenPairDto>> LoginAsync(string account, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrEmpty(password))
        {
            return AuthResult<TokenPairDto>.Failure("invalid_credentials", "The account or password is incorrect.");
        }

        var normalizedAccount = Normalize(account);
        var user = await FindUserWithRolesAsync(normalizedAccount, cancellationToken);
        if (user is null)
        {
            return AuthResult<TokenPairDto>.Failure("invalid_credentials", "The account or password is incorrect.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (user.LockoutEndUtc > now)
        {
            return AuthResult<TokenPairDto>.Failure("account_locked", "The account is temporarily locked.");
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedAccessCount++;
            if (user.FailedAccessCount >= MaximumFailedAccessAttempts)
            {
                user.LockoutEndUtc = now.Add(LockoutDuration);
                user.FailedAccessCount = 0;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return AuthResult<TokenPairDto>.Failure("invalid_credentials", "The account or password is incorrect.");
        }

        user.FailedAccessCount = 0;
        user.LockoutEndUtc = null;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
        }

        var pair = IssueTokenPair(user, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.SetProfileAsync(pair.User, cancellationToken);
        await cache.SetAuthenticationVersionAsync(user.Id, user.AuthenticationVersion, cancellationToken);
        return AuthResult<TokenPairDto>.Success(pair);
    }

    public async Task<AuthResult<TokenPairDto>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthResult<TokenPairDto>.Failure("invalid_refresh_token", "The refresh token is invalid or expired.");
        }

        var tokenHash = HashToken(refreshToken);
        var token = await dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .Include(item => item.User)
                .ThenInclude(user => user.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (token is null || token.IsDeleted || token.User.IsDeleted || token.ExpiresAtUtc <= now)
        {
            return AuthResult<TokenPairDto>.Failure("invalid_refresh_token", "The refresh token is invalid or expired.");
        }

        if (token.RevokedAtUtc is not null)
        {
            if (token.ReplacedByTokenHash is not null)
            {
                await RevokeAllTokensAsync(token.User, now, cancellationToken);
            }

            return AuthResult<TokenPairDto>.Failure("refresh_token_reused", "Refresh token reuse was detected.");
        }

        var newRefreshToken = CreateRefreshToken(token.User, now);
        token.RevokedAtUtc = now;
        token.ReplacedByTokenHash = newRefreshToken.TokenHash;
        dbContext.RefreshTokens.Add(newRefreshToken.Entity);

        var pair = CreateTokenPair(token.User, newRefreshToken, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.SetProfileAsync(pair.User, cancellationToken);
        return AuthResult<TokenPairDto>.Success(pair);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = HashToken(refreshToken);
        var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
        if (token is not null && token.RevokedAtUtc is null)
        {
            token.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        await RevokeAllTokensAsync(user, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }

    public async Task<AuthResult<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cached = await cache.GetProfileAsync(userId, cancellationToken);
        if (cached is not null)
        {
            return AuthResult<UserProfileDto>.Success(cached);
        }

        var user = await dbContext.Users
            .Include(item => item.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return AuthResult<UserProfileDto>.Failure("user_not_found", "The user no longer exists.");
        }

        var profile = mapper.Map<UserProfileDto>(user);
        await cache.SetProfileAsync(profile, cancellationToken);
        return AuthResult<UserProfileDto>.Success(profile);
    }

    public async Task<bool> IsCurrentAsync(Guid userId, int authenticationVersion, CancellationToken cancellationToken)
    {
        var cachedVersion = await cache.GetAuthenticationVersionAsync(userId, cancellationToken);
        if (cachedVersion.HasValue)
        {
            return cachedVersion.Value == authenticationVersion;
        }

        var currentVersion = await dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => (int?)user.AuthenticationVersion)
            .SingleOrDefaultAsync(cancellationToken);
        if (!currentVersion.HasValue)
        {
            return false;
        }

        await cache.SetAuthenticationVersionAsync(userId, currentVersion.Value, cancellationToken);
        return currentVersion.Value == authenticationVersion;
    }

    private async Task<User?> FindUserWithRolesAsync(string normalizedAccount, CancellationToken cancellationToken) =>
        await dbContext.Users
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(
                user => user.NormalizedUserName == normalizedAccount || user.NormalizedEmail == normalizedAccount,
                cancellationToken);

    private TokenPairDto IssueTokenPair(User user, DateTime now)
    {
        var refreshToken = CreateRefreshToken(user, now);
        dbContext.RefreshTokens.Add(refreshToken.Entity);
        return CreateTokenPair(user, refreshToken, now);
    }

    private TokenPairDto CreateTokenPair(User user, RefreshTokenEnvelope refreshToken, DateTime now)
    {
        var options = jwtOptions.Value;
        var accessExpiresAt = now.AddMinutes(options.AccessTokenMinutes);
        var roles = user.UserRoles
            .Where(userRole => !userRole.IsDeleted && !userRole.Role.IsDeleted)
            .Select(userRole => userRole.Role.Name)
            .OrderBy(role => role)
            .ToArray();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new("auth_version", user.AuthenticationVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            notBefore: now,
            expires: accessExpiresAt,
            signingCredentials: credentials);
        var profile = mapper.Map<UserProfileDto>(user);

        return new TokenPairDto(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            accessExpiresAt,
            refreshToken.PlainTextToken,
            refreshToken.Entity.ExpiresAtUtc,
            profile);
    }

    private RefreshTokenEnvelope CreateRefreshToken(User user, DateTime now)
    {
        var plainTextToken = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            UserId = user.Id,
            User = user,
            TokenHash = HashToken(plainTextToken),
            ExpiresAtUtc = now.AddDays(jwtOptions.Value.RefreshTokenDays)
        };
        return new RefreshTokenEnvelope(entity, plainTextToken);
    }

    private async Task RevokeAllTokensAsync(User user, DateTime now, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == user.Id && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var activeToken in activeTokens)
        {
            activeToken.RevokedAtUtc = now;
        }

        user.AuthenticationVersion++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(user.Id, cancellationToken);
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static bool IsValidEmail(string email)
    {
        try
        {
            return new MailAddress(email).Address.Equals(email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };

    private sealed record RefreshTokenEnvelope(RefreshToken Entity, string PlainTextToken)
    {
        public string TokenHash => Entity.TokenHash;
    }
}
