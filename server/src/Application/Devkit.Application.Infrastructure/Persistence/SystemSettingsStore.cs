using Devkit.Application.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devkit.Application.Infrastructure.Persistence;

internal sealed class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.ToTable("sys_system_settings", table => table.HasCheckConstraint("ck_sys_system_settings_singleton", "id = 1"));
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.TimeZone).HasMaxLength(SystemSettings.TimeZoneMaxLength);
        builder.HasData(new SystemSettings());
    }
}

internal sealed class SystemSettingsStore(ApplicationDbContext db) : ISystemSettingsStore
{
    public Task<string?> GetTimeZoneAsync(CancellationToken ct) => db.Set<SystemSettings>()
        .Where(settings => settings.Id == SystemSettings.SingletonId)
        .Select(settings => settings.TimeZone)
        .SingleAsync(ct);

    public async Task SaveTimeZoneAsync(string? timeZone, CancellationToken ct)
    {
        var settings = await db.Set<SystemSettings>().SingleAsync(row => row.Id == SystemSettings.SingletonId, ct);
        if (settings.TimeZone == timeZone) return;

        settings.TimeZone = timeZone;
        await db.SaveChangesAsync(ct);
    }
}
