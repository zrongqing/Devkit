using System.Text.Json;
using Devkit.Business.Infrastructure.Persistence;
using Devkit.FileStorage.Infrastructure.Persistence;
using Devkit.Identity.Infrastructure.Persistence;
using Devkit.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class DatabaseConfigurationTests
{
    [Fact]
    public void Shared_defaults_and_all_design_time_factories_select_the_same_database()
    {
        using var configuration = LoadDefaults();
        Assert.Equal("Devkit", new SqlConnectionStringBuilder(DatabaseConfiguration.GetConnectionString(configuration)).InitialCatalog);
        var args = new[] { "--ConnectionStrings:Devkit=Server=design.test;Database=Devkit;Integrated Security=true" };
        using var identity = new IdentityDbContextFactory().CreateDbContext(args);
        using var business = new BusinessDbContextFactory().CreateDbContext(args);
        using var files = new FileStorageDbContextFactory().CreateDbContext(args);
        Assert.Equal("Devkit", identity.Database.GetDbConnection().Database);
        Assert.Equal("Devkit", business.Database.GetDbConnection().Database);
        Assert.Equal("Devkit", files.Database.GetDbConnection().Database);
    }

    [Fact]
    public void Shared_defaults_replace_old_service_specific_database_settings()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Devkit"] = "Server=old.test;Database=OldSharedDatabase"
        });
        configuration.AddDatabaseConfiguration([], externalFile: "");
        var providers = ((IConfigurationRoot)configuration).Providers.ToArray();
        var shared = Array.FindIndex(providers, p => p.TryGet("ConnectionStrings:Devkit", out var value)
            && value!.Contains("Database=Devkit;", StringComparison.Ordinal));
        var old = Array.FindIndex(providers, p => p.TryGet("ConnectionStrings:Devkit", out var value)
            && value == "Server=old.test;Database=OldSharedDatabase");
        Assert.True(shared > old);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Server=sql.test")]
    [InlineData("Database=Devkit")]
    [InlineData("Server=sql.test;Database=Devkit;AttachDbFilename=test.mdf")]
    [InlineData("invalid=secret-password")]
    [InlineData("Server=sql.test;Database=Devkit;Integrated Security=secret-password")]
    [InlineData("Server=sql.test;Database=Devkit;Connect Timeout=secret-password")]
    public void Missing_or_invalid_targets_are_rejected_without_disclosing_connection_values(string? value)
    {
        using var configuration = LoadDefaults();
        configuration["ConnectionStrings:Devkit"] = value;
        var error = Assert.Throws<InvalidOperationException>(() => DatabaseConfiguration.GetConnectionString(configuration));
        Assert.Contains("ConnectionStrings:Devkit", error.Message);
        Assert.DoesNotContain("secret-password", error.ToString());
    }

    [Fact]
    public void Shared_file_overrides_host_settings_and_command_line_remains_highest_priority()
    {
        var root = Path.Combine(Path.GetTempPath(), $"devkit-db-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "shared.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new { ConnectionStrings = new
            {
                Devkit = "Server=shared.test;Database=Devkit"
            } }));
            using var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Devkit"] = "host-settings" });
            configuration.AddDatabaseConfiguration([
                "--ConnectionStrings:Devkit=Server=cli.test;Database=Devkit" ], externalFile: path);
            Assert.Equal("cli.test", new SqlConnectionStringBuilder(DatabaseConfiguration.GetConnectionString(configuration)).DataSource);
            // Inspect providers to establish file order without depending on ambient environment variables.
            var providers = ((IConfigurationRoot)configuration).Providers.ToArray();
            var hostIndex = Array.FindIndex(providers, p => p.TryGet("ConnectionStrings:Devkit", out var v) && v == "host-settings");
            var sharedIndex = Array.FindIndex(providers, p => p.TryGet("ConnectionStrings:Devkit", out var v) && v == "Server=shared.test;Database=Devkit");
            Assert.True(sharedIndex > hostIndex);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Explicit_missing_shared_file_fails_instead_of_falling_back_to_local_database()
    {
        using var configuration = new ConfigurationManager();
        Assert.Throws<FileNotFoundException>(() => configuration.AddDatabaseConfiguration([],
            externalFile: Path.Combine(Path.GetTempPath(), $"devkit-missing-{Guid.NewGuid():N}.json")));
    }

    [Theory]
    [InlineData("Identity")]
    [InlineData("Business")]
    [InlineData("FileStorage")]
    public async Task All_hosts_accept_the_shared_database_without_opening_a_connection(string name)
    {
        using var configuration = LoadDefaults();
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            if (name == "Identity") services.AddDevkitSqlServer<IdentityDbContext>(configuration, name);
            else if (name == "Business") services.AddDevkitSqlServer<BusinessDbContext>(configuration, name);
            else services.AddDevkitSqlServer<FileStorageDbContext>(configuration, name);
        }).Build();
        await host.StartAsync();
        using var scope = host.Services.CreateScope();
        var db = name switch
        {
            "Identity" => (DbContext)scope.ServiceProvider.GetRequiredService<IdentityDbContext>(),
            "Business" => scope.ServiceProvider.GetRequiredService<BusinessDbContext>(),
            _ => scope.ServiceProvider.GetRequiredService<FileStorageDbContext>()
        };
        Assert.Equal("Devkit", db.Database.GetDbConnection().Database);
        await host.StopAsync();
    }

    [Fact]
    public async Task Invalid_shared_connection_fails_before_host_accepts_requests()
    {
        using var configuration = LoadDefaults();
        configuration["ConnectionStrings:Devkit"] = "";
        using var host = new HostBuilder().ConfigureServices(services =>
            services.AddDevkitSqlServer<BusinessDbContext>(configuration, "Business")).Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
    }

    private static ConfigurationManager LoadDefaults()
    {
        var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, DatabaseConfiguration.FileName));
        return configuration;
    }
}
