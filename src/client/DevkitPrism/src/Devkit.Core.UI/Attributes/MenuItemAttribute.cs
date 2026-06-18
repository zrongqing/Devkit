namespace Devkit.Core.UI.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class MenuItemAttribute : Attribute
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string IconPath { get; set; }
    public string ParentId { get; set; }
    public int Order { get; set; }
    public string RequiredPermission { get; set; }

    // 二选一：View-first 或 ViewModel-first
    public string ViewName { get; set; }
}