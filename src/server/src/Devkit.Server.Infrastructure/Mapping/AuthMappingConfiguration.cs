using Devkit.Server.Application.Contracts.Auth;
using Devkit.Server.Domain.Identity;
using Mapster;

namespace Devkit.Server.Infrastructure.Mapping;

internal static class AuthMappingConfiguration
{
    public static TypeAdapterConfig Create()
    {
        var config = new TypeAdapterConfig();
        config.NewConfig<User, UserProfileDto>()
            .Map(destination => destination.Roles,
                source => source.UserRoles
                    .Where(userRole => !userRole.IsDeleted && !userRole.Role.IsDeleted)
                    .Select(userRole => userRole.Role.Name)
                    .OrderBy(role => role)
                    .ToArray());
        config.Compile();
        return config;
    }
}
