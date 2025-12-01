using IBAPI.ExecuteMilestone.Model;
using Microsoft.IdentityModel.Protocols;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using VideoOS.Platform;
using VideoOS.Platform.Data;

public static class MilestoneServices
{
    private static readonly Guid IntegrationId = new Guid("B03477E2-CCFA-4E44-9092-292960128807");
    private const string IntegrationName = "PTZ and Presets";
    private const string Version = "1.0";
    private const string ManufacturerName = "Sample Manufacturer";
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    private const string FormatDatetime = "yyyy-MM-dd HH:mm:ss";
    private static ResponseModel login()
    {
        var rs = new ResponseModel();
        try
        {
            Uri uri = new Uri(ConfigurationManager.AppSettings["MileStone_Url"]);
            if (!VideoOS.Platform.SDK.Environment.IsLoggedIn(uri))
            {
                string milestoneBin = @"C:\Program Files\Milestone\XProtect Recording Server";
                Environment.SetEnvironmentVariable("PATH",
                    Environment.GetEnvironmentVariable("PATH") + ";" + milestoneBin);

                VideoOS.Platform.SDK.Environment.Initialize();        
                VideoOS.Platform.SDK.UI.Environment.Initialize();
                VideoOS.Platform.SDK.Export.Environment.Initialize();

                EnvironmentManager.Instance.TraceFunctionCalls = true;
                NetworkCredential network = new NetworkCredential(ConfigurationManager.AppSettings["MileStone_Account"], ConfigurationManager.AppSettings["MileStone_Password"]);
                CredentialCache cc = VideoOS.Platform.Login.Util.BuildCredentialCache(uri, ConfigurationManager.AppSettings["MileStone_Account"], ConfigurationManager.AppSettings["MileStone_Password"], "Basic");
                VideoOS.Platform.SDK.Environment.AddServer(false, uri, cc);
                VideoOS.Platform.SDK.Environment.Login(uri, IntegrationId, IntegrationName, Version, ManufacturerName);
            }

            rs.Status = VideoOS.Platform.SDK.Environment.IsLoggedIn(uri);
        }
        catch (Exception ex)
        {
            log.Error(ex);
            rs.Status = false;
            rs.Message = "Không đăng nhập được";
        }
        return rs;
    }

    public static async Task<ResponseModel> ExportCameraVideo(ExportVideoInfor param)
    {
        var rs = new ResponseModel();
        IExporter _exporter = null;
        var _cameraItems = new List<Item>();
        var fileName = "";
        var typeFile = ".avi";
        try
        {
            // Đăng nhập Milestone Server
            login();

            var cameraItem = VideoOS.Platform.Configuration.Instance.GetItem(param.CameraId, Kind.Camera);
            if (cameraItem == null)
            {
                rs.Status = false;
                rs.Message = "Camera không hợp lệ hoặc không tìm thấy.";
                return rs;
            }
            _cameraItems.Add(cameraItem);

            if (!DateTime.TryParseExact(param.StartTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateStart) ||
                !DateTime.TryParseExact(param.EndTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateEnd))
            {
                rs.Status = false;
                rs.Message = "Định dạng thời gian không hợp lệ (phải là yyyy-MM-dd HH:mm:ss)";
                return rs;
            }

            if (dateStart >= dateEnd)
            {
                rs.Status = false;
                rs.Message = "Thời gian bắt đầu phải nhỏ hơn thời gian kết thúc.";
                return rs;
            }

            string destPath = Path.Combine(param.DestPathVideo, MakeStringPathValid(cameraItem.Name));
            Directory.CreateDirectory(destPath);

            string fileType = (param.FileOutType ?? "MKV").ToUpperInvariant();
            switch (fileType)
            {
                case "AVI":
                    var aviExporter = new AVIExporter
                    {
                        Filename = MakeStringPathValid(param.FileName),
                        Codec = "H.264",
                        //AudioSampleRate = 8000
                    };
                    _exporter = aviExporter;
                    fileName = aviExporter.Filename;
                    typeFile = ".avi";
                    break;

                case "MKV":
                    var mkvExporter = new MKVExporter
                    {
                        Filename = MakeStringPathValid(param.FileName)
                    };
                    _exporter = mkvExporter;
                    typeFile = ".mkv";
                    break;

                default:
                    rs.Status = false;
                    rs.Message = $"Định dạng xuất '{param.FileOutType}' không được hỗ trợ. (Chỉ hỗ trợ AVI hoặc MKV)";
                    return rs;
            }
            if (string.IsNullOrEmpty(destPath)) 
            {
                rs.Status = false;
                rs.Message = $"Path url wrong";
            }
            else
            {
                _exporter.Init();
                _exporter.Path = destPath;
                _exporter.CameraList.AddRange(_cameraItems);
                //_exporter.AudioList.AddRange(audioSources);

                if (_exporter.StartExport(dateStart.ToUniversalTime(), dateEnd.ToUniversalTime()))
                {
                    Console.WriteLine($"Path URL Export: {destPath}");

                    bool exportDone = false;

                    while (_exporter.LastError == 0 && !exportDone)
                    {
                        await Task.Delay(100); 

                        int progress = _exporter.Progress;
                        int lastError = _exporter.LastError;

                        Console.WriteLine($"Export progress: {progress}%, lastError: {lastError}");
                        
                        if (progress >= 100)
                        {
                            exportDone = true;
                            rs.Status = true;
                            rs.Message = "Success!";
                            rs.UrlPath = _exporter.Path + "\\" + fileName + typeFile;
                        }
                    }

                    if (_exporter.LastError != 0)
                    {
                        rs.Status = false;
                        rs.Message = $"Lỗi khi xuất video: {_exporter.LastErrorString} (Mã lỗi: {_exporter.LastError})";
                        log.ErrorFormat("ExportCameraVideo lỗi: " + rs.Message);
                    }
                }
                else
                {
                    rs.Status = false;
                    rs.Message = $"Không thể bắt đầu xuất video: {_exporter.LastErrorString} (Mã lỗi: {_exporter.LastError})";
                    log.ErrorFormat("ExportCameraVideo lỗi: " + rs.Message);
                }
            }
        }
        catch (NoVideoInTimeSpanMIPException ex)
        {
            rs.Status = false;
            rs.Message = $"Không có video trong khoảng thời gian đã chọn: {ex.Message}";
            log.Error("ExportCameraVideo NoVideoInTimeSpanMIPException", ex);
        }
        catch (Exception ex)
        {
            rs.Status = false;
            rs.Message = $"Lỗi hệ thống khi xuất video: {ex.Message}";
            log.Error("ExportCameraVideo Exception", ex);
        }
        finally
        {
            if (_exporter != null)
            {
                try
                {
                    _exporter.EndExport();
                    _exporter.Close();
                }
                catch { }
                _exporter = null;
            }
        }

        return rs;
    }

    private static string MakeStringPathValid(string unsafeString)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string result = unsafeString;
        foreach (var invalidCharacter in invalidCharacters)
        {
            result = result.Replace(invalidCharacter, '_');
        }
        return result;
    }
}
