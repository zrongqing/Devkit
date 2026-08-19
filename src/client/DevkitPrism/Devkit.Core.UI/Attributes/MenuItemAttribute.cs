namespace Devkit.Core.UI.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class MenuItemAttribute : Attribute
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string ParentId { get; set; } = string.Empty;
    public int Order { get; set; } = int.MinValue;
    public string RequiredPermission { get; set; } = string.Empty;
    // 二选一：View-first 或 ViewModel-first
    public string ViewName { get; set; } = string.Empty;
}