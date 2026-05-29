using Devkit.Contracts.Services;
using Devkit.Helpers;
using Devkit.ViewModels;
using Devkit.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Devkit.Services;

public class PageService : IPageService
{
    private readonly Dictionary<string, Type> _pages = new Dictionary<string, Type>();
    private readonly IServiceProvider _serviceProvider;

    public PageService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Configure<MainViewModel, MainPage>();
    }

    public Type GetPageType(string key)
    {
        Type pageType;
        lock (_pages)
        {
            if (!_pages.TryGetValue(key, out pageType))
            {
                throw new ArgumentException($"Page not found: {key}. Did you forget to call PageService.Configure?");
            }
        }

        return pageType;
    }

    public Page GetPage(string key)
    {
        var pageType = GetPageType(key);
        return _serviceProvider.GetService(pageType) as Page;
    }

    private void Configure<VM, V>()
        where VM : INotifyPropertyChanged
        where V : Page
    {
        lock (_pages)
        {
            var key = typeof(VM).FullName;
            if (_pages.ContainsKey(key))
            {
                throw new ArgumentException($"The key {key} is already configured in PageService");
            }

            var type = typeof(V);
            if (_pages.Any(p => p.Value == type))
            {
                throw new ArgumentException($"This type is already configured with key {_pages.First(p => p.Value == type).Key}");
            }

            _pages.Add(key, type);
        }
    }

    // 用于模块卸载：移除已注册的页面映射
    public void UnregisterPageForViewModel(Type viewModelType)
    {
        lock (_pages)
        {
            var key = viewModelType.FullName;
            _pages.Remove(key);
        }
    }

    // 可以按需暴露另一个重载
    public void UnregisterPage<VM>() where VM : Observable
    {
        UnregisterPageForViewModel(typeof(VM));
    }
}
