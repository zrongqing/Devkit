using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts.Navigation;
using Devkit.Server.Application.Navigation;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.Navigation;

namespace Devkit.Server.Infrastructure.Navigation;

internal sealed class WebMenuDefaults(INavigationMenuService navigation) : IWebMenuDefaults
{
    public IReadOnlyList<WebMenu> Create()
    {
        var configured = navigation.GetMenus(NavigationAudience.Web);
        var codes = configured.ToDictionary(x => x.Id, x => BusinessCode(string.IsNullOrEmpty(x.TargetKey) ? x.Id : x.TargetKey));
        return configured.Select(x => {
            var declaration = new MenuDeclaration(codes[x.Id], string.IsNullOrEmpty(x.TargetKey) ? "directory" : "module",
                x.ParentId is null ? null : codes[x.ParentId], x.Title, string.IsNullOrEmpty(x.TargetKey) ? null : x.TargetKey,
                x.IconKey, x.Order, x.RequiredPermission is null ? [] : [x.RequiredPermission], x.IsClosable);
            var menu = WebMenuService.FromDeclaration(declaration);
            menu.IsPublic = menu.Type == "module" && x.RequiredPermission is null;
            return menu;
        }).ToArray();
    }
    // Only the first-install legacy configuration uses this mapping. Runtime menus live in the database.
    private static string BusinessCode(string legacy) => legacy switch
    {
        "study-project-management" => "knowledge.projects",
        "study-knowledge" => "knowledge.bases",
        "study-projects" => "knowledge.search",
        "study-practice" => "knowledge.practice.exam",
        "study-questions" => "knowledge.practice.questions",
        "study-progress" => "knowledge.practice.progress",
        "study-jobs" => "system.monitor.jobs",
        "system-users" => "system.identity.users",
        "system-roles" => "system.identity.roles",
        "system-identity" => "system.identity.permissions",
        "system-menus" => "system.menus",
        "system-files" => "system.files",
        "system-storage" => "system.storage",
        "system-status" => "system.monitor.status",
        "settings" => "system.settings",
        "about" => "system.about",
        "system-management" => "system.management",
        "system-management.users" => "system.identity",
        "system" => "system.monitor",
        _ => legacy
    };
}
