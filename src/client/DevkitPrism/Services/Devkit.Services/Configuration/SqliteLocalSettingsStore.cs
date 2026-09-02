using System.Security.Cryptography;
using System.Text;
using Devkit.Services.Interfaces.Configuration;
using Microsoft.Data.Sqlite;

namespace Devkit.Services.Configuration;

public sealed class SqliteLocalSettingsStore : ILocalSettingsStore, IDisposable
{
    private static readonly byte[] OptionalEntropy =
        Encoding.UTF8.GetBytes("Devkit.LocalSettings.v1");

    private readonly string _databasePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public SqliteLocalSettingsStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
    }

    public static string GetDefaultDatabasePath()
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("无法确定当前用户的 LocalApplicationData 目录。");
        }

        return Path.Combine(
            localApplicationData,
            "Devkit",
            "Config",
            "devkit-settings.db");
    }

    public async Task<IReadOnlyDictionary<string, string>> ReadScopeAsync(
        string scope,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(scope);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT SettingKey, SettingValue, IsProtected
                              FROM Settings
                              WHERE Scope = $scope;
                              """;
        command.Parameters.AddWithValue("$scope", scope);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.GetString(0);
            var storedValue = reader.GetString(1);
            result[key] = reader.GetBoolean(2)
                              ? Unprotect(storedValue)
                              : storedValue;
        }

        return result;
    }

    public async Task WriteScopeAsync(
        string scope,
        IReadOnlyCollection<LocalSettingValue> settings,
        CancellationToken cancellationToken = default)
    {
        ValidateSettings(scope, settings);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await EnsureSchemaAsync(connection, cancellationToken);
            await using var transaction =
                (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await UpsertSettingsAsync(connection, transaction, scope, settings, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> IsMigrationAppliedAsync(
        string scope,
        int version,
        CancellationToken cancellationToken = default)
    {
        ValidateMigration(scope, version);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT 1
                              FROM SettingsMigrations
                              WHERE Scope = $scope AND Version = $version
                              LIMIT 1;
                              """;
        command.Parameters.AddWithValue("$scope", scope);
        command.Parameters.AddWithValue("$version", version);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task<bool> ApplyMigrationAsync(
        string scope,
        int version,
        IReadOnlyCollection<LocalSettingValue> settings,
        CancellationToken cancellationToken = default)
    {
        ValidateMigration(scope, version);
        ValidateSettings(scope, settings);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await EnsureSchemaAsync(connection, cancellationToken);
            await using var transaction =
                (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            await using (var check = connection.CreateCommand())
            {
                check.Transaction = transaction;
                check.CommandText = """
                                    SELECT 1
                                    FROM SettingsMigrations
                                    WHERE Scope = $scope AND Version = $version
                                    LIMIT 1;
                                    """;
                check.Parameters.AddWithValue("$scope", scope);
                check.Parameters.AddWithValue("$version", version);
                if (await check.ExecuteScalarAsync(cancellationToken) is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }
            }

            await UpsertSettingsAsync(connection, transaction, scope, settings, cancellationToken);

            await using (var marker = connection.CreateCommand())
            {
                marker.Transaction = transaction;
                marker.CommandText = """
                                     INSERT INTO SettingsMigrations (Scope, Version, AppliedUtc)
                                     VALUES ($scope, $version, $appliedUtc);
                                     """;
                marker.Parameters.AddWithValue("$scope", scope);
                marker.Parameters.AddWithValue("$version", version);
                marker.Parameters.AddWithValue("$appliedUtc", DateTimeOffset.UtcNow.ToString("O"));
                await marker.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose() => _writeLock.Dispose();

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_databasePath)
                        ?? throw new InvalidOperationException("SQLite 配置数据库路径无效。");
        Directory.CreateDirectory(directory);

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task EnsureSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              PRAGMA journal_mode = WAL;
                              CREATE TABLE IF NOT EXISTS Settings (
                                  Scope TEXT NOT NULL,
                                  SettingKey TEXT NOT NULL,
                                  SettingValue TEXT NOT NULL,
                                  IsProtected INTEGER NOT NULL,
                                  UpdatedUtc TEXT NOT NULL,
                                  PRIMARY KEY (Scope, SettingKey)
                              );
                              CREATE TABLE IF NOT EXISTS SettingsMigrations (
                                  Scope TEXT NOT NULL,
                                  Version INTEGER NOT NULL,
                                  AppliedUtc TEXT NOT NULL,
                                  PRIMARY KEY (Scope, Version)
                              );
                              """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertSettingsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string scope,
        IEnumerable<LocalSettingValue> settings,
        CancellationToken cancellationToken)
    {
        foreach (var setting in settings)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                                  INSERT INTO Settings (
                                      Scope, SettingKey, SettingValue, IsProtected, UpdatedUtc)
                                  VALUES (
                                      $scope, $key, $value, $isProtected, $updatedUtc)
                                  ON CONFLICT(Scope, SettingKey) DO UPDATE SET
                                      SettingValue = excluded.SettingValue,
                                      IsProtected = excluded.IsProtected,
                                      UpdatedUtc = excluded.UpdatedUtc;
                                  """;
            command.Parameters.AddWithValue("$scope", scope);
            command.Parameters.AddWithValue("$key", setting.Key);
            command.Parameters.AddWithValue(
                "$value",
                setting.IsSecret ? Protect(setting.Value) : setting.Value);
            command.Parameters.AddWithValue("$isProtected", setting.IsSecret ? 1 : 0);
            command.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static string Protect(string value)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("本地秘密配置依赖 Windows DPAPI。");
        }

        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(value),
            OptionalEntropy,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string Unprotect(string value)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("本地秘密配置依赖 Windows DPAPI。");
        }

        try
        {
            var decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(value),
                OptionalEntropy,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            throw new InvalidDataException(
                "本地配置中的受保护字段无法由当前 Windows 用户解密，请重新配置。",
                exception);
        }
    }

    private static void ValidateScope(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
    }

    private static void ValidateMigration(string scope, int version)
    {
        ValidateScope(scope);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }
    }

    private static void ValidateSettings(
        string scope,
        IReadOnlyCollection<LocalSettingValue> settings)
    {
        ValidateScope(scope);
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Any(setting => string.IsNullOrWhiteSpace(setting.Key)))
        {
            throw new ArgumentException("配置键不能为空。", nameof(settings));
        }

        if (settings.Select(setting => setting.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != settings.Count)
        {
            throw new ArgumentException("同一批次中不能包含重复配置键。", nameof(settings));
        }
    }
}
