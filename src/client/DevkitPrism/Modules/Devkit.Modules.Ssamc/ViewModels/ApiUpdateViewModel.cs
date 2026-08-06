using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devkit.Core.UI.Mvvm;
using Devkit.Services.Interfaces;
using HandyControl.Controls;
using Module.Ssamc.Configuration;
using Module.Ssamc.Servers;
using Ssamc.Core.ApiCodeCollector;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Module.Ssamc.ViewModels;

public partial class ApiUpdateViewModel : ViewModelBase
{
    private string _message;
    public string Message
    {
        get { return _message; }
        set { SetProperty(ref _message, value); }
    }
    
    private ApiUpdateServer _apiUpdateServer;
    private IFileService _fileService;
    private IModuleStorage _moduleStorage;

    /// <summary>
    /// 源代码路径
    /// </summary>
    [ObservableProperty]
    private string _sourceCodePath = SsamcEnvironment.SourceCodePath;
    /// <summary>
    /// 源代码路径下的全部apicode
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _allApiCodes = new();

    private IList<string> _selectedApiCodes = new List<string>();
    public IList<string> SelectApiCodes
    {
        get => _selectedApiCodes;
        set => SetProperty(ref _selectedApiCodes, value);
    }

    [ObservableProperty]
    private string _strSelectApiCodes = string.Empty;
    [ObservableProperty]
    private string _codePreview = string.Empty;
    [ObservableProperty]
    private string _sourceCodePreview = string.Empty;
    [ObservableProperty]
    private string _strUpdateApis = string.Empty;

    private List<ApiSourceInfo> _apiSourceInfos = new List<ApiSourceInfo>();

    public ApiUpdateViewModel(
        ApiUpdateServer apiUpdateServer,
        IFileService fileService,
        IModuleStorage moduleStorage)
    {
        Message = "View A from your Prism Module";
        _apiUpdateServer = apiUpdateServer;
        _fileService = fileService;
        _moduleStorage = moduleStorage;

        LoadFile();
    }

    [RelayCommand]
    private void ScanSourceCode()
    {
        var allApiSourceInfos = _apiUpdateServer.GetAllApiSourceInfos(this.SourceCodePath);
        _apiSourceInfos = allApiSourceInfos;
        
        var apiCodes = _apiUpdateServer.GetAllApiCodes(allApiSourceInfos);
        this.AllApiCodes = new ObservableCollection<string>(apiCodes);
        this.SelectApiCodes = new ObservableCollection<string>(apiCodes);
        SaveFile();
    }

    [RelayCommand]
    private void Preview()
    {
        try
        {
            // var apiCode = this.SelectApiCodes.FirstOrDefault();
            // if (string.IsNullOrEmpty(apiCode)) return;
            var apiCode = this.StrSelectApiCodes;

            this.SourceCodePreview = _apiUpdateServer.GetExecutionSourceCodeByApiCode(
                sourcePath: this.SourceCodePath,
                apiCode: apiCode);
            this.CodePreview = _apiUpdateServer.GetSourceCodeByApiCode(
                sourcePath: this.SourceCodePath,
                apiCode: apiCode);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    [RelayCommand]
    private void TabClick()
    {
        try
        {

        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    [RelayCommand]
    private async Task Update(object btnParam)
    {
        var updateApiCodes = new List<string>();
        if(!string.IsNullOrEmpty(StrUpdateApis))
        {
            var temps = StrUpdateApis.Split(';').ToList();
            updateApiCodes.AddRange(temps);
        }

        if(!updateApiCodes.Any())
        {
            Growl.Info("请输入要更新的接口");
            return;
        }
        await Task.Run(() =>
        {
            try
            {
                var target = btnParam?.ToString()
                    ?? throw new InvalidOperationException("未指定数据库目标环境。");
                var connectionString = SsamcEnvironment.GetDatabaseConnection(target);

                var apiInfos = _apiUpdateServer.GetAllApiSourceInfos(this.SourceCodePath);
                foreach (var apiCode in updateApiCodes)
                {
                    var sourceCode = _apiUpdateServer.GetSourceCodeByApiCode(apiInfos, apiCode);
                    var result = _apiUpdateServer.UpdateExtendCode(apiCode, sourceCode, connectionString);

                    var strResult = string.Empty;
                    if (result)
                    {
                        strResult = $@"{apiCode}，更新成功";
                    }
                    else
                    {
                        strResult = $@"{apiCode}，更新失败";
                    }
                    Growl.Info(strResult.ToString());
                }
            }
            catch(Exception ex)
            {
                Growl.Info(ex.ToString());
            }
        });
        //ApiUpdate
        SaveFile();
    }

    private void SaveFile()
    {
        try
        {
            var folderPath = _moduleStorage.GetModulePath("ssamc");
            dynamic saveData = new ExpandoObject();
            
            saveData.SourceCodePath = this.SourceCodePath;
            saveData.StrSelectApiCodes = this.StrSelectApiCodes;

            _fileService.Save(folderPath, "apiupdate.json", saveData);
        }
        catch
        {

        }
    }

    private void LoadFile()
    {
        try
        {
            var folderPath = _moduleStorage.GetModulePath("ssamc");
            var saveData = _fileService.Read<dynamic>(folderPath, "apiupdate.json");

            this.SourceCodePath = saveData.SourceCodePath;

            //var allApiCodes = (List<string>)saveData.AllApiCodes;
            //this.AllApiCodes = new ObservableCollection<string>(allApiCodes);

            //var selectApiCodes = (List<string>)saveData.SelectApiCodes;
            //this.SelectApiCodes = new ObservableCollection<string>(selectApiCodes);

            this.StrSelectApiCodes = saveData.StrSelectApiCodes;
        }
        catch(Exception ex)
        {

        }
        finally
        {

        }
    }
}
