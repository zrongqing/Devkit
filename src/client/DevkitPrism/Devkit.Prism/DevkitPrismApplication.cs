using System.Data;
using System.Windows;
using Devkit.Services;
using Devkit.Services.Interfaces;
using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Prism;
using Prism.Ioc;
using Prism.DryIoc;

namespace Devkit.Prism;

class MyClass : PrismApplication
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

abstract class DevkitBasePrismApplication : PrismApplicationBase
{
    /// <summary>
    /// Create <see cref="T:DryIoc.Rules" /> to alter behavior of <see cref="T:DryIoc.IContainer" />
    /// </summary>
    /// <returns>An instance of <see cref="T:DryIoc.Rules" /></returns>
    protected virtual Rules CreateContainerRules() => DryIocContainerExtension.DefaultRules;

    /// <summary>
    /// Create a new <see cref="T:DryIocContainerExtension" /> used by Prism.
    /// </summary>
    /// <returns>A new <see cref="T:DryIocContainerExtension" />.</returns>
    protected override IContainerExtension CreateContainerExtension()
    {
        return (IContainerExtension) new DryIocContainerExtension((IContainer) new DryIoc.Container(this.CreateContainerRules()));
    }

    /// <summary>
    /// Registers the <see cref="T:System.Type" />s of the Exceptions that are not considered
    /// root exceptions by the <see cref="T:System.ExceptionExtensions" />.
    /// </summary>
    protected override void RegisterFrameworkExceptionTypes()
    {
        ExceptionExtensions.RegisterFrameworkExceptionType(typeof (ContainerException));
    }
}

public abstract class DevkitPrismApplication : PrismApplication
{

    /// <summary>
    /// Create a new <see cref="T:DryIocContainerExtension" /> used by Prism.
    /// 自定义使用的容器
    /// </summary>
    /// <returns>A new <see cref="T:DryIocContainerExtension" />.</returns>
    protected override IContainerExtension CreateContainerExtension()
    {
        // 适配Microsoft.Extensions.DependencyInjection，将微软容器中的东西加入到DryIoc中
        var microsoftServers = GetPrismServiceCollection();

        Rules ruls = this.CreateContainerRules();
        var container = new DryIoc.Container(this.CreateContainerRules());
        container.WithDependencyInjectionAdapter(microsoftServers);
    
        return new DryIocContainerExtension(container);
    }

    private IServiceCollection _serviceCollection = new ServiceCollection();
    /// <summary>
    /// Microsoft.Extensions.DependencyInjection 容器
    /// </summary>
    /// <returns></returns>
    protected virtual IServiceCollection GetPrismServiceCollection() => _serviceCollection;
}
