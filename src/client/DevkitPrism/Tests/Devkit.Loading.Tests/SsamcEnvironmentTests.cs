using Ssamc.Configuration;
using Xunit;

namespace Devkit.Loading.Tests;

[Collection("Ssamc environment")]
public sealed class SsamcEnvironmentTests
{
    [Fact]
    public void Share_targets_use_built_in_roots_and_usernames()
    {
        var development = Assert.Single(SsamcEnvironment.ResolveShareTargets("215.22", _ => null));
        var test = Assert.Single(SsamcEnvironment.ResolveShareTargets("20.53", _ => null));
        var production = SsamcEnvironment.ResolveShareTargets("production", _ => null);

        Assert.Equal(@"\\192.168.215.22\assets_huatek\webapp", development.Root);
        Assert.Equal("Administrator", development.Username);
        Assert.Equal(@"\\192.168.20.53\webapp", test.Root);
        Assert.Equal("huatek", test.Username);
        Assert.Equal(
            [
                @"\\192.168.209.22\assets_huatek\webapp",
                @"\\192.168.209.39\assets_huatek\webapp",
                @"\\192.168.10.41\assets_huatek\webapp"
            ],
            production.Select(target => target.Root));
        Assert.All(production, target => Assert.Equal("huatek", target.Username));
    }

    [Fact]
    public void Share_target_environment_values_override_built_in_mapping()
    {
        var settings = new Dictionary<string, string?>
        {
            ["WEBAPP_PRODUCTION_ROOTS"] = @"\\server-a\webapp; \\server-b\webapp",
            ["WEBAPP_PRODUCTION_USERNAME"] = "configured-user",
            ["WEBAPP_PRODUCTION_PASSWORD"] = "configured-password"
        };

        var targets = SsamcEnvironment.ResolveShareTargets(
            "production",
            suffix => settings.GetValueOrDefault(suffix));

        Assert.Equal([@"\\server-a\webapp", @"\\server-b\webapp"], targets.Select(target => target.Root));
        Assert.All(targets, target =>
        {
            Assert.Equal("configured-user", target.Username);
            Assert.Equal("configured-password", target.Password);
        });
    }

    [Fact]
    public void Share_target_without_password_allows_current_windows_identity()
    {
        var settings = new Dictionary<string, string?>
        {
            ["WEBAPP_UNCONFIGURED_ROOTS"] = @"\\unconfigured\webapp",
            ["WEBAPP_UNCONFIGURED_USERNAME"] = "unconfigured-user"
        };

        var target = Assert.Single(SsamcEnvironment.ResolveShareTargets(
            "unconfigured",
            suffix => settings.GetValueOrDefault(suffix)));

        Assert.Empty(target.Password);
    }

    [Fact]
    public void Environment_resolution_falls_back_from_process_to_user_then_machine()
    {
        var values = new Dictionary<EnvironmentVariableTarget, string?>
        {
            [EnvironmentVariableTarget.Process] = null,
            [EnvironmentVariableTarget.User] = " user-value ",
            [EnvironmentVariableTarget.Machine] = "machine-value"
        };
        var requestedTargets = new List<EnvironmentVariableTarget>();

        var value = SsamcEnvironment.ResolveEnvironmentVariable(
            "TEST_VARIABLE",
            (_, target) =>
            {
                requestedTargets.Add(target);
                return values[target];
            });

        Assert.Equal(" user-value ", value);
        Assert.Equal(
            [EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User],
            requestedTargets);
    }

}
