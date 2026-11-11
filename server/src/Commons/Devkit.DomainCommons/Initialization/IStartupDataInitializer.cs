namespace Devkit.DomainCommons.Initialization;

/// <summary>Ensures a module's required data exists. Implementations must tolerate repeated
/// execution, including concurrent starts of multiple service instances.</summary>
public interface IStartupDataInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
