using System.Windows;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Devkit.Prism;

internal class MyClass : PrismApplication
{
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        throw new NotImplementedException();
    }
    protected override Window CreateShell()
    {
        throw new NotImplementedException();
    }
}

internal abstract class DevkitBasePrismApplication : PrismApplicationBase
{
    /// <summary>
    /// Create <see cref="T:DryIoc.Rules" /> to alter behavior of <see cref="T:DryIoc.IContainer" />
    /// </summary>
    /// <returns> An instance of <see cref="T:DryIoc.Rules" /> </returns>
    protected virtual Rules CreateContainerRules()
    {
        return DryIocContainerExtension.DefaultRules;
    }

    /// <summary>
    /// Create a new <see cref="T:DryIocContainerExtension" /> used by Prism.
    /// </summary>
    /// <returns> A new <see cref="T:DryIocContainerExtension" />. </returns>
    protected override IContainerExtension CreateContainerExtension()
    {
        return new DryIocContainerExtension(new Container(CreateContainerRules()));
    }

    /// <summary>
    /// Registers the <see cref="T:System.Type" />s of the Exceptions that are not considered
    /// root exceptions by the <see cref="T:System.ExceptionExtensions" />.
    /// </summary>
    protected override void RegisterFrameworkExceptionTypes()
    {
        ExceptionExtensions.RegisterFrameworkExceptionType(typeof(ContainerException));
    }
}

public abstract class DevkitPrismApplication : PrismApplication
{
    private readonly IServiceCollection _serviceCollection = new ServiceCollection();
    protected IContainerProvider _containerProvider;
    protected override IContainerExtension CreateContainerExtension()
    {
        // 适配Microsoft.Extensions.DependencyInjection，将微软容器中的东西加入到DryIoc中
        var microsoftServers = GetPrismServiceCollection();

        var ruls = CreateContainerRules();
        var container = new Container(CreateContainerRules());
        container.WithDependencyInjectionAdapter(microsoftServers);

        var containerExtension = new DryIocContainerExtension(container);
        _containerProvider = (IContainerProvider)containerExtension;
        return containerExtension;
    }
    protected virtual IServiceCollection GetPrismServiceCollection()
    {
        return _serviceCollection;
    }
}
