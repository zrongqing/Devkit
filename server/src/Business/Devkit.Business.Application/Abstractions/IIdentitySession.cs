using Devkit.Business.Application.Workspace;

namespace Devkit.Business.Application.Abstractions;

// The business context consumes an identity assertion through a versioned HTTP contract.
// No identity entity, EF context, or authentication cache crosses this boundary.
public interface IIdentitySession
{
    Actor? Current { get; }
    Task AuthenticateAsync(string accessToken, Guid expectedSubject, CancellationToken cancellationToken);
}
