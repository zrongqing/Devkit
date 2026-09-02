namespace Devkit.Services.Interfaces.Configuration;

/// <summary>
/// Stores user-local application settings in named scopes.
/// </summary>
public interface ILocalSettingsStore
{
    Task<IReadOnlyDictionary<string, string>> ReadScopeAsync(
        string scope,
        CancellationToken cancellationToken = default);

    Task WriteScopeAsync(
        string scope,
        IReadOnlyCollection<LocalSettingValue> settings,
        CancellationToken cancellationToken = default);

    Task<bool> IsMigrationAppliedAsync(
        string scope,
        int version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes initial settings and records the migration in the same transaction.
    /// Returns false when the migration has already been applied.
    /// </summary>
    Task<bool> ApplyMigrationAsync(
        string scope,
        int version,
        IReadOnlyCollection<LocalSettingValue> settings,
        CancellationToken cancellationToken = default);
}

