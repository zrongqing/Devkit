using Devkit.Core.UI.Contracts;
using Devkit.Core.UI.Models;
using Devkit.Core.UI.Mvvm;
using Devkit.Core.UI.Services;
using Devkit.Prism.Events;
using Devkit.Services.Interfaces.Logging;
using Devkit.ViewModels;
using Moq;
using Prism.Events;
using Xunit;

namespace Devkit.Loading.Tests;

public class MenuTabViewModelTests
{
    [Fact]
    public async Task New_tab_is_created_immediately_and_single_tab_is_reused()
    {
        var loadable = new BlockingLoadable();
        var context = CreateContext(_ => loadable);

        context.Events.GetEvent<MenuClickEvent>().Publish(context.Menu.Id);

        var tab = context.ViewModel.Tabs.Single();
        Assert.Same(tab, context.ViewModel.SelectedTab);
        Assert.Same(loadable, tab.Content);
        Assert.Equal(1, loadable.InitializeCount);
        await WaitUntilAsync(() => context.GlobalLoading.IsVisible);

        loadable.Complete();
        await WaitUntilAsync(() => !context.GlobalLoading.IsBusy);
        Assert.False(tab.HasLoadError);

        context.Events.GetEvent<MenuClickEvent>().Publish(context.Menu.Id);
        Assert.Single(context.ViewModel.Tabs);
        Assert.Equal(1, loadable.InitializeCount);
        context.ViewModel.UnloadedCommand.Execute();
    }

    [Fact]
    public async Task Failed_tab_is_retained_and_retry_uses_fresh_content()
    {
        var retryLoadable = new BlockingLoadable();
        var resolveCount = 0;
        var context = CreateContext(_ =>
            Interlocked.Increment(ref resolveCount) == 1
                ? new FailingLoadable()
                : retryLoadable);

        context.Events.GetEvent<MenuClickEvent>().Publish(context.Menu.Id);
        var tab = context.ViewModel.Tabs.Single();
        await WaitUntilAsync(() => tab.HasLoadError);

        Assert.Contains("模块加载失败", tab.LoadErrorMessage);
        Assert.Single(context.ViewModel.Tabs);

        context.ViewModel.RetryTabCommand.Execute(tab);
        await WaitUntilAsync(() => retryLoadable.InitializeCount == 1);
        Assert.Same(retryLoadable, tab.Content);

        retryLoadable.Complete();
        await WaitUntilAsync(() => !context.GlobalLoading.IsBusy);
        Assert.False(tab.HasLoadError);
        Assert.Equal(2, resolveCount);
        context.ViewModel.UnloadedCommand.Execute();
    }

    [Fact]
    public async Task Closing_tab_cancels_module_initialization()
    {
        var loadable = new BlockingLoadable();
        var context = CreateContext(_ => loadable);

        context.Events.GetEvent<MenuClickEvent>().Publish(context.Menu.Id);
        var tab = context.ViewModel.Tabs.Single();
        Assert.Equal(1, loadable.InitializeCount);
        context.ViewModel.CloseTabCommand.Execute(tab);

        Assert.Empty(context.ViewModel.Tabs);
        await loadable.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => !context.GlobalLoading.IsBusy);
        context.ViewModel.UnloadedCommand.Execute();
    }

    private static TestContext CreateContext(Func<MenuItemModel, object> contentFactory)
    {
        var menu = new MenuItemModel
        {
            Id = "test-module",
            Title = "Test module",
            ViewName = "TestView",
            IsClosable = true
        };
        var shellService = new Mock<IShellService>();
        shellService.Setup(service => service.FindMenu("home")).Returns((MenuItemModel?)null);
        shellService.Setup(service => service.FindMenu(menu.Id)).Returns(menu);
        shellService.Setup(service => service.ResolveContent(menu)).Returns(() => contentFactory(menu));

        var events = new EventAggregator();
        var globalLoading = new DelayedLoadingState(TimeSpan.FromMilliseconds(20));
        var viewModel = new MenuTabViewModel(
            events,
            shellService.Object,
            globalLoading,
            Mock.Of<IClientLogger>());
        viewModel.LoadedCommand.Execute();

        return new TestContext(viewModel, events, globalLoading, menu);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(5, timeout.Token);
        }
    }

    private sealed record TestContext(
        MenuTabViewModel ViewModel,
        EventAggregator Events,
        DelayedLoadingState GlobalLoading,
        MenuItemModel Menu);

    private sealed class FailingLoadable : IAsyncLoadable
    {
        public Task InitializeAsync(CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("load failed"));
    }

    private sealed class BlockingLoadable : IAsyncLoadable
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Canceled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int InitializeCount { get; private set; }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            InitializeCount++;
            try
            {
                await _completion.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Canceled.TrySetResult();
                throw;
            }
        }

        public void Complete() => _completion.TrySetResult();
    }
}
