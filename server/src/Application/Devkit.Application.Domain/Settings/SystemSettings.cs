namespace Devkit.Application.Domain.Settings;

public sealed class SystemSettings
{
    public const int SingletonId = 1;
    public const int TimeZoneMaxLength = 128;

    public int Id { get; init; } = SingletonId;
    public string? TimeZone { get; set; }
}

public interface ISystemSettingsStore
{
    Task<string?> GetTimeZoneAsync(CancellationToken ct);
    Task SaveTimeZoneAsync(string? timeZone, CancellationToken ct);
}
