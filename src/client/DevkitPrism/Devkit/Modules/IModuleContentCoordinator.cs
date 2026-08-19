using Devkit.ViewModels;

namespace Devkit.Modules;

public interface IModuleContentCoordinator
{
    void Attach(MenuTabViewModel viewModel);

    void Detach(MenuTabViewModel viewModel);

    int GetOpenContentCount(string moduleId);

    Task CloseModuleContentAsync(string moduleId);
}

public sealed class ModuleContentCoordinator : IModuleContentCoordinator
{
    private WeakReference<MenuTabViewModel>? _viewModel;

    public void Attach(MenuTabViewModel viewModel) => _viewModel = new WeakReference<MenuTabViewModel>(viewModel);

    public void Detach(MenuTabViewModel viewModel)
    {
        if (_viewModel?.TryGetTarget(out var current) == true && ReferenceEquals(current, viewModel))
        {
            _viewModel = null;
        }
    }

    public int GetOpenContentCount(string moduleId) =>
        _viewModel?.TryGetTarget(out var viewModel) == true
            ? viewModel.GetOpenModuleContentCount(moduleId)
            : 0;

    public Task CloseModuleContentAsync(string moduleId) =>
        _viewModel?.TryGetTarget(out var viewModel) == true
            ? viewModel.CloseModuleContentAsync(moduleId)
            : Task.CompletedTask;
}
