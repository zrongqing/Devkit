using Devkit.FileStorage.Application.Files;

namespace Devkit.FileStorage.Application.Abstractions;

// The file storage context consumes an identity assertion through a versioned HTTP contract.
// No identity entity, EF context, or authentication cache crosses this boundary.
public interface IIdentitySession
{
    Actor? Current { get; }
    Task AuthenticateAsync(string accessToken, Guid expectedSubject, CancellationToken cancellationToken);
}
