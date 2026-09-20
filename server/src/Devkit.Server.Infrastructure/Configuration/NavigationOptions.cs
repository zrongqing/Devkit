namespace Devkit.Server.Infrastructure.Configuration;

public sealed class NavigationOptions
{
    public const string SectionName = "Navigation";

    public List<NavigationMenuOption> Common { get; set; } = [];
    public List<NavigationMenuOption> Web { get; set; } = [];
    public List<NavigationMenuOption> Client { get; set; } = [];
}

public sealed class NavigationMenuOption
{
    public string Id { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public string? IconKey { get; set; }
    public int Order { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsClosable { get; set; } = true;
}
