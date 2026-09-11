namespace Devkit.Services.Interfaces.Dialogs;

public interface IConfirmationDialogService
{
    Task<bool> ConfirmAsync(
        string message,
        string title = "请确认",
        string confirmText = "确认",
        string cancelText = "取消");
}
