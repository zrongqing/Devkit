using Devkit.Application.Application.Authorization;
using Devkit.Application.Domain.Settings;

namespace Devkit.Application.Application.Settings;

public sealed class SystemSettingsService(ISystemSettingsStore store)
{
    public const string ManagePermission = "system.settings.manage";

    public Task<string?> GetTimeZoneAsync(CancellationToken ct) => store.GetTimeZoneAsync(ct);

    public async Task<string?> UpdateTimeZoneAsync(string? timeZone, CancellationToken ct)
    {
        if (timeZone is not (null or "UTC") && !IsRegionTimeZone(timeZone))
        {
            throw new IdentityException(400, "invalid_time_zone", "请选择有效的 IANA 地区时区或 UTC。")
            {
                Field = "timeZone"
            };
        }

        await store.SaveTimeZoneAsync(timeZone, ct);
        return timeZone;
    }

    private static bool IsRegionTimeZone(string timeZone) =>
        timeZone.Length <= SystemSettings.TimeZoneMaxLength &&
        timeZone.Contains('/') &&
        !timeZone.StartsWith("Etc/", StringComparison.OrdinalIgnoreCase) &&
        TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZone, out _);
}
