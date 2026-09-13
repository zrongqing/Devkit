using Devkit.Server.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devkit.Server.Infrastructure.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedUserName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasPrecision(7);
        builder.Property(x => x.UpdatedAtUtc).HasPrecision(7);
        builder.HasIndex(x => x.NormalizedUserName).IsUnique();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasPrecision(7);
        builder.Property(x => x.UpdatedAtUtc).HasPrecision(7);
        builder.HasIndex(x => x.NormalizedName).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedAtUtc).HasPrecision(7);
        builder.Property(x => x.UpdatedAtUtc).HasPrecision(7);
        builder.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
        builder.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class UserClaimConfiguration : IEntityTypeConfiguration<UserClaim>
{
    public void Configure(EntityTypeBuilder<UserClaim> builder)
    {
        builder.ToTable("UserClaims");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClaimType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ClaimValue).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasPrecision(7);
        builder.Property(x => x.UpdatedAtUtc).HasPrecision(7);
        builder.HasIndex(x => x.UserId);
        builder.HasOne(x => x.User).WithMany(x => x.UserClaims).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class RoleClaimConfiguration : IEntityTypeConfiguration<RoleClaim>
{
    public void Configure(EntityTypeBuilder<RoleClaim> builder)
    {
        builder.ToTable("RoleClaims");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClaimType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ClaimValue).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasPrecision(7);
        builder.Property(x => x.UpdatedAtUtc).HasPrecision(7);
        builder.HasIndex(x => x.RoleId);
        builder.HasOne(x => x.Role).WithMany(x => x.RoleClaims).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class UserLoginConfiguration : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> builder)
    {
        builder.ToTable("UserLogins");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LoginProvider).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ProviderKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ProviderDisplayName).HasMaxLength(256);
        builder.Property(x => x.CreatedAtUtc).HasPrecision(7);
        builder.Property(x => x.UpdatedAtUtc).HasPrecision(7);
        builder.HasIndex(x => new { x.LoginProvider, x.ProviderKey }).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasOne(x => x.User).WithMany(x => x.UserLogins).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.ToTable("UserTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LoginProvider).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasPrecision(7);
        builder.Property(x => x.UpdatedAtUtc).HasPrecision(7);
        builder.HasIndex(x => new { x.UserId, x.LoginProvider, x.Name }).IsUnique();
        builder.HasOne(x => x.User).WithMany(x => x.UserTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
        builder.Property(x => x.CreatedAtUtc).HasPrecision(7);
        builder.Property(x => x.UpdatedAtUtc).HasPrecision(7);
        builder.Property(x => x.ExpiresAtUtc).HasPrecision(7);
        builder.Property(x => x.RevokedAtUtc).HasPrecision(7);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
        builder.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
