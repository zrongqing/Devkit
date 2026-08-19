namespace Devkit.Prism.Modules;

/// <summary>
/// Gives a dynamically loaded Prism module a chance to release registrations
/// or process-wide resources before its load context is unloaded.
/// </summary>
public interface IUnloadableModule
{
    void OnUnloading(IContainerProvider containerProvider);
}
