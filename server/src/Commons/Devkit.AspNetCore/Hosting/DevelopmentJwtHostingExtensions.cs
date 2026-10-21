using Devkit.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Devkit.AspNetCore.Hosting;

public static class DevelopmentJwtHostingExtensions
{
    /// <summary>
    /// 添加开发环境下JWTKEY
    /// </summary>
    /// <remarks>开发环境下会自动生成jwt数据</remarks>
    public static WebApplicationBuilder AddDevelopmentJwtKey(this WebApplicationBuilder builder, bool signing)
    {
        var configurationKey = signing ? "Jwt:SigningPrivateKeyPath" : "Jwt:ValidationPublicKeyPath";
        if (!builder.Environment.IsDevelopment() || !string.IsNullOrWhiteSpace(builder.Configuration[configurationKey]))
            return builder;

        var directory = builder.Configuration["Jwt:DevelopmentKeyDirectory"];
        if (string.IsNullOrWhiteSpace(directory))
        {
            var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localData))
                throw new InvalidOperationException("Local application data is unavailable. Configure Jwt:DevelopmentKeyDirectory or an explicit JWT key path.");
            directory = Path.Combine(localData, "Devkit", "development", "jwt");
        }

        var keys = DevelopmentJwtKeyStore.GetOrCreate(directory);
        builder.Configuration[configurationKey] = signing ? keys.PrivateKeyPath : keys.PublicKeyPath;
        return builder;
    }
}
