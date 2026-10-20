using Devkit.Infrastructure.Security;
using System.Security.Cryptography;
using Devkit.FileStorage.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Devkit.FileStorage.Infrastructure.Identity;

public sealed class JwtValidationKey : IDisposable
{
    private readonly RSA rsa;
    public SecurityKey ValidationKey { get; }
    public string Algorithm => SecurityAlgorithms.RsaSha256;

    public JwtValidationKey(IOptions<JwtOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ValidationPublicKeyPath))
            throw new InvalidOperationException("Jwt:ValidationPublicKeyPath is required by the business service.");
        rsa = RsaKeyLoader.ReadPublicKey(options.Value.ValidationPublicKeyPath);
        ValidationKey = RsaKeyLoader.CreateSecurityKey(rsa);
    }

    public void Dispose() => rsa.Dispose();
}
