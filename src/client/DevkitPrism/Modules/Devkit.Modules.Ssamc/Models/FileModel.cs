using CommunityToolkit.Mvvm.ComponentModel;

namespace Ssamc.Models;

public partial class FileModel : ObservableObject
{
    [ObservableProperty]
    private string _fileExtension = string.Empty;
    [ObservableProperty]
    private string _fileName = string.Empty;
    [ObservableProperty]
    private string _filePath = string.Empty;
}
