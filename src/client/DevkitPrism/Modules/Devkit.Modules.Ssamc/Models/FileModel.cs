using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ssamc.Models;

public partial class FileModel : ObservableObject
{
    [ObservableProperty]
    private string _fileName;
    [ObservableProperty]
    private string _fileExtension;
    [ObservableProperty]
    private string _filePath;
}
