using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Devkit.Sharp.Contracts;

public interface IModule
{
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
    Task OnInitializedAsync(IServiceProvider provider);
}
