using System;

using Devkit.Models;

namespace Devkit.Contracts.Services;

public interface IThemeSelectorService
{
    bool SetTheme(AppTheme? theme = null);

    AppTheme GetCurrentTheme();
}
