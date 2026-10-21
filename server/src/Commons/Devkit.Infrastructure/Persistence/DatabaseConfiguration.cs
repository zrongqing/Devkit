using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Devkit.Infrastructure.Persistence;

public static class DatabaseConfiguration
{
    public const string FileName = "database.json";
    public const string FileEnvironmentVariable = "DEVKIT_DATABASE_CONFIG_FILE";
    public const string ConnectionName = "Devkit";

    /// <summary>
    /// 添加数据库配置文件
    /// </summary>
    public static IConfigurationBuilder AddDatabaseConfiguration(this IConfigurationBuilder builder,
        string[] args, string? defaultDirectory = null, string? externalFile = null)
    {
        // Database settings have one file entry point for hosts and EF factories.
        builder.AddJsonFile(Path.Combine(defaultDirectory ?? AppContext.BaseDirectory, FileName),
            optional: false, reloadOnChange: false);

        var path = externalFile ?? Environment.GetEnvironmentVariable(FileEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(path))
            builder.AddJsonFile(Path.GetFullPath(path), optional: false, reloadOnChange: false);

        return builder.AddEnvironmentVariables().AddCommandLine(args);
    }

    public static string GetConnectionString(IConfiguration configuration)
    {
        var selected = configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(selected))
            throw new InvalidOperationException($"ConnectionStrings:{ConnectionName} is required.");

        SqlConnectionStringBuilder connection;
        try { connection = new SqlConnectionStringBuilder(selected); }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            throw new InvalidOperationException($"ConnectionStrings:{ConnectionName} must be a valid SQL Server connection string.");
        }
        if (string.IsNullOrWhiteSpace(connection.DataSource) || string.IsNullOrWhiteSpace(connection.InitialCatalog)
            || !string.IsNullOrWhiteSpace(connection.AttachDBFilename))
            throw new InvalidOperationException($"ConnectionStrings:{ConnectionName} must specify Server and Database without AttachDbFilename.");
        return selected;
    }

    public static string GetDesignTimeConnectionString(string[] args)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddDatabaseConfiguration(args);
        return GetConnectionString(configuration);
    }

}
