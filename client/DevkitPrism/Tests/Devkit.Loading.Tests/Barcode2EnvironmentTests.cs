using Barcode2.Configuration;
using Xunit;

namespace Devkit.Loading.Tests;

[Collection("Barcode2 environment")]
public sealed class Barcode2EnvironmentTests
{
    [Fact]
    public void Defaults_use_semantic_keys_and_blank_external_values()
    {
        var settings = Barcode2Defaults.Create();

        Assert.Equal(
            [
                Barcode2Defaults.ProductionEnvironment,
                Barcode2Defaults.TestEnvironment,
                Barcode2Defaults.DevelopmentEnvironment
            ],
            settings.Environments.Select(environment => environment.Key));
        Assert.All(settings.Environments, environment =>
        {
            Assert.Empty(environment.DatabaseDataSource);
            Assert.Empty(environment.DatabaseUsername);
            Assert.Empty(environment.DatabasePassword);
            Assert.Empty(environment.PageBaseAddress);
        });
        Assert.All(settings.ShareTargets, target =>
        {
            Assert.Empty(target.Root);
            Assert.Empty(target.Username);
            Assert.Empty(target.Password);
        });
    }

    [Theory]
    [InlineData("production", Barcode2Defaults.ProductionEnvironment)]
    [InlineData("test", Barcode2Defaults.TestEnvironment)]
    [InlineData("development", Barcode2Defaults.DevelopmentEnvironment)]
    public void Semantic_aliases_resolve_to_stable_environment_keys(
        string alias,
        string expected)
    {
        Assert.Equal(expected, Barcode2Defaults.ResolveEnvironmentKey(alias));
    }

    [Fact]
    public void Unknown_alias_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Barcode2Defaults.ResolveEnvironmentKey("legacy-alias"));
    }

    [Fact]
    public void Invalid_page_address_is_rejected()
    {
        var settings = Barcode2Defaults.Create();
        settings.Environments[0].PageBaseAddress = "file:///c:/temp";

        var exception = Assert.Throws<Barcode2ConfigurationException>(() =>
            Barcode2ConfigurationService.Validate(settings));

        Assert.Contains("HTTP", exception.Message);
    }
}
