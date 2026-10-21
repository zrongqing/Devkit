using System.Security.Cryptography;
using Devkit.AspNetCore.Hosting;
using Devkit.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Devkit.Server.Api.IntegrationTests;

public sealed class DevelopmentJwtKeyTests : IDisposable
{
    private readonly string testRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("DEVKIT_TEST_ROOT") ?? Path.GetTempPath());
    private readonly string directory;

    public DevelopmentJwtKeyTests() => directory = Path.Combine(testRoot, $"devkit-jwt-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Development_hosts_share_persisted_keys_even_when_verifiers_start_first()
    {
        var business = Builder(Environments.Development);
        var files = Builder(Environments.Development);
        var identity = Builder(Environments.Development);
        business.AddDevelopmentJwtKey(signing: false);
        var privatePath = Path.Combine(directory, "identity-private.pem");
        var originalPrivate = File.ReadAllBytes(privatePath);
        var publicPath = business.Configuration["Jwt:ValidationPublicKeyPath"]!;
        var originalPublic = File.ReadAllBytes(publicPath);
        files.AddDevelopmentJwtKey(signing: false);
        identity.AddDevelopmentJwtKey(signing: true);

        using var issuer = new Devkit.Identity.Infrastructure.Identity.JwtKeyMaterial(Options.Create(
            identity.Configuration.GetSection("Jwt").Get<Devkit.Identity.Infrastructure.Configuration.JwtOptions>()!));
        using var businessKeys = new Devkit.Business.Infrastructure.Identity.JwtValidationKey(Options.Create(
            business.Configuration.GetSection("Jwt").Get<Devkit.Business.Infrastructure.Configuration.JwtOptions>()!));
        using var fileKeys = new Devkit.FileStorage.Infrastructure.Identity.JwtValidationKey(Options.Create(
            files.Configuration.GetSection("Jwt").Get<Devkit.FileStorage.Infrastructure.Configuration.JwtOptions>()!));
        Assert.Equal(issuer.ValidationKey.KeyId, businessKeys.ValidationKey.KeyId);
        Assert.Equal(issuer.ValidationKey.KeyId, fileKeys.ValidationKey.KeyId);
        using var signer = RsaKeyLoader.ReadPrivateKey(privatePath);
        using var verifier = RsaKeyLoader.ReadPublicKey(publicPath);
        Assert.Equal(3072, signer.KeySize);
        var payload = RandomNumberGenerator.GetBytes(64);
        Assert.True(verifier.VerifyData(payload, signer.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        Assert.ThrowsAny<CryptographicException>(() => verifier.ExportParameters(true));

        Builder(Environments.Development).AddDevelopmentJwtKey(signing: true);
        Assert.Equal(originalPrivate, File.ReadAllBytes(privatePath));
        Assert.Equal(originalPublic, File.ReadAllBytes(publicPath));
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(privatePath));
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", true)]
    [InlineData("Staging", false)]
    [InlineData("Testing", true)]
    [InlineData("Testing", false)]
    public void Other_environments_still_require_explicit_key_paths(string environment, bool signing)
    {
        var builder = Builder(environment);
        builder.AddDevelopmentJwtKey(signing);
        Assert.Null(builder.Configuration[signing ? "Jwt:SigningPrivateKeyPath" : "Jwt:ValidationPublicKeyPath"]);
        Assert.False(Directory.Exists(directory));
        if (signing)
            Assert.Throws<InvalidOperationException>(() => new Devkit.Identity.Infrastructure.Identity.JwtKeyMaterial(
                Options.Create(new Devkit.Identity.Infrastructure.Configuration.JwtOptions())));
        else
        {
            Assert.Throws<InvalidOperationException>(() => new Devkit.Business.Infrastructure.Identity.JwtValidationKey(
                Options.Create(new Devkit.Business.Infrastructure.Configuration.JwtOptions())));
            Assert.Throws<InvalidOperationException>(() => new Devkit.FileStorage.Infrastructure.Identity.JwtValidationKey(
                Options.Create(new Devkit.FileStorage.Infrastructure.Configuration.JwtOptions())));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Explicit_paths_are_not_replaced_even_when_the_file_is_missing(bool signing)
    {
        var builder = Builder(Environments.Development);
        var key = signing ? "Jwt:SigningPrivateKeyPath" : "Jwt:ValidationPublicKeyPath";
        var configured = Path.Combine(directory, "explicit-missing.pem");
        builder.Configuration[key] = configured;
        builder.AddDevelopmentJwtKey(signing);
        Assert.Equal(configured, builder.Configuration[key]);
        Assert.False(Directory.Exists(directory));
        Directory.CreateDirectory(directory);
        Assert.Throws<FileNotFoundException>(() => signing ? RsaKeyLoader.ReadPrivateKey(configured) : RsaKeyLoader.ReadPublicKey(configured));
    }

    [Fact]
    public async Task Simultaneous_initialization_uses_one_complete_key_pair()
    {
        var pairs = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => DevelopmentJwtKeyStore.GetOrCreate(directory))));
        Assert.Single(pairs.Distinct());
        using var privateKey = RsaKeyLoader.ReadPrivateKey(pairs[0].PrivateKeyPath);
        using var publicKey = RsaKeyLoader.ReadPublicKey(pairs[0].PublicKeyPath);
        Assert.Equal(RsaKeyLoader.CreateSecurityKey(privateKey).KeyId, RsaKeyLoader.CreateSecurityKey(publicKey).KeyId);
        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }

    [Fact]
    public void Missing_public_key_is_recovered_without_rotating_the_private_key()
    {
        var pair = DevelopmentJwtKeyStore.GetOrCreate(directory);
        var privateBytes = File.ReadAllBytes(pair.PrivateKeyPath);
        var publicBytes = File.ReadAllBytes(pair.PublicKeyPath);
        File.Delete(pair.PublicKeyPath);
        DevelopmentJwtKeyStore.GetOrCreate(directory);
        Assert.Equal(privateBytes, File.ReadAllBytes(pair.PrivateKeyPath));
        Assert.Equal(publicBytes, File.ReadAllBytes(pair.PublicKeyPath));
    }

    [Theory]
    [InlineData("missing-private", typeof(InvalidOperationException))]
    [InlineData("invalid-private", typeof(ArgumentException))]
    [InlineData("invalid-public", typeof(ArgumentException))]
    [InlineData("mismatched-public", typeof(InvalidOperationException))]
    public void Damaged_existing_keys_fail_without_silently_replacing_them(string defect, Type exceptionType)
    {
        var pair = DevelopmentJwtKeyStore.GetOrCreate(directory);
        switch (defect)
        {
            case "missing-private": File.Delete(pair.PrivateKeyPath); break;
            case "invalid-private": File.WriteAllText(pair.PrivateKeyPath, "invalid"); break;
            case "invalid-public": File.WriteAllText(pair.PublicKeyPath, "invalid"); break;
            case "mismatched-public":
                using (var other = RSA.Create(2048)) File.WriteAllText(pair.PublicKeyPath, other.ExportSubjectPublicKeyInfoPem());
                break;
        }
        var privateBytes = File.Exists(pair.PrivateKeyPath) ? File.ReadAllBytes(pair.PrivateKeyPath) : null;
        var publicBytes = File.ReadAllBytes(pair.PublicKeyPath);
        Assert.Throws(exceptionType, () => DevelopmentJwtKeyStore.GetOrCreate(directory));
        Assert.Equal(privateBytes, File.Exists(pair.PrivateKeyPath) ? File.ReadAllBytes(pair.PrivateKeyPath) : null);
        Assert.Equal(publicBytes, File.ReadAllBytes(pair.PublicKeyPath));
    }

    private WebApplicationBuilder Builder(string environment)
    {
        // Empty builders never read the developer's User Secrets, configuration files, or key directory.
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration["Jwt:DevelopmentKeyDirectory"] = directory;
        return builder;
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(directory);
        if (!resolved.StartsWith(testRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith("devkit-jwt-tests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove a directory outside this test's storage.");
        if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
    }
}
