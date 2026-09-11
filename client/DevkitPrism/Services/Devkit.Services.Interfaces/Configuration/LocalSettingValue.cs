namespace Devkit.Services.Interfaces.Configuration;

public sealed record LocalSettingValue(
    string Key,
    string Value,
    bool IsSecret = false);

