using IBAPI.ExecuteMilestone.Model;
using IBAPI.MetadataMilestone.Model;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VideoOS.Platform;
using VideoOS.Platform.Client;
using VideoOS.Platform.Data;
using VideoOS.Platform.Live;
using VideoOS.Platform.Messaging;
using VideoOS.Platform.UI;

public static class MilestoneServices
{
    private static readonly Guid IntegrationId = new Guid("B03477E2-CCFA-4E44-9092-292960128809");
    private const string IntegrationName = "PTZ and Presets";
    private const string Version = "1.0";
    private const string ManufacturerName = "Sample Manufacturer";
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    private static MetadataLiveSource _metadataLiveSource;
    private static Item _selectItem1;

    private static readonly object _loginLock = new object();
    private static bool _isInitialized;
    private static readonly object _metadataLock = new object();
    private static readonly HttpClient _httpClient =
        new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

    private static FQID _playbackFQID;

    public static void Initialize()
    {
        _playbackFQID = ClientControl.Instance.GeneratePlaybackController();
        EnvironmentManager.Instance.RegisterReceiver(PlaybackTimeChangedHandler,
                                             new MessageIdFilter(MessageId.SmartClient.PlaybackCurrentTimeIndication));
    }

    private static object PlaybackTimeChangedHandler(VideoOS.Platform.Messaging.Message message, FQID dest, FQID sender)
    {
        // Only pick up messages coming from my own PlaybackController (sender is null for the common PlaybackController)
        if (_playbackFQID.EqualGuids(sender))
        {
            var time = (DateTime)message.Data;
            Debug.WriteLine("PlaybackTimeChangedHandler: " + time.ToLongTimeString());

            TimeChangedHandler(time);

        }
        return null;
    }

    private static void TimeChangedHandler(DateTime time)
    {
        //if (_currentShownTime != time)
        //{
        //    _nextToFetchTime = time;
        //    Debug.WriteLine("TimeChangedHandler: " + _nextToFetchTime.ToLongTimeString());
        //}
    }

    private static ResponseModel EnsureLogin()
    {
        var rs = new ResponseModel();

        try
        {
            Uri uri = new Uri(ConfigurationManager.AppSettings["MileStone_Url"]);

            if (VideoOS.Platform.SDK.Environment.IsLoggedIn(uri))
            {
                rs.Status = true;
                return rs;
            }

            lock (_loginLock)
            {
                if (VideoOS.Platform.SDK.Environment.IsLoggedIn(uri))
                {
                    rs.Status = true;
                    return rs;
                }

                if (!_isInitialized)
                {
                    string milestoneBin = @"C:\Program Files\Milestone\XProtect Recording Server";
                    Environment.SetEnvironmentVariable(
                        "PATH",
                        Environment.GetEnvironmentVariable("PATH") + ";" + milestoneBin
                    );

                    VideoOS.Platform.SDK.Environment.Initialize();
                    VideoOS.Platform.SDK.UI.Environment.Initialize();
                    VideoOS.Platform.SDK.Media.Environment.Initialize();
                    VideoOS.Platform.SDK.Export.Environment.Initialize();

                    _isInitialized = true;
                }

                CredentialCache cc = VideoOS.Platform.Login.Util.BuildCredentialCache(
                    uri,
                    ConfigurationManager.AppSettings["MileStone_Account"],
                    ConfigurationManager.AppSettings["MileStone_Password"],
                    "Basic"
                );

                VideoOS.Platform.SDK.Environment.AddServer(false, uri, cc);
                VideoOS.Platform.SDK.Environment.Login(
                    uri,
                    IntegrationId,
                    IntegrationName,
                    Version,
                    ManufacturerName
                );
            }

            rs.Status = VideoOS.Platform.SDK.Environment.IsLoggedIn(uri);
        }
        catch (Exception ex)
        {
            log.Error(ex);
            rs.Status = false;
            rs.Message = "Không đăng nhập được Milestone";
        }

        return rs;
    }

    public static ResponseModel GetMetadataLiveViewer(MetadataInput param)
    {
        var rs = new ResponseModel();

        try
        {
            var loginResult = EnsureLogin();
            if (!loginResult.Status)
                return loginResult;

            lock (_metadataLock)
            {
                //if (_metadataLiveSource != null &&
                //    _selectItem1?.FQID == param.MetadataId)
                //{
                //    // Đã init rồi → reuse
                //    rs.Status = true;
                //    return rs;
                //}

                // Close source cũ
                if (_metadataLiveSource != null)
                {
                    _metadataLiveSource.LiveContentEvent -= OnLiveContentEvent;
                    _metadataLiveSource.Close();
                    _metadataLiveSource = null;
                }
                
                _selectItem1 = VideoOS.Platform.Configuration.Instance
                    .GetItem(param.MetadataId, Kind.Metadata);

                _metadataLiveSource = new MetadataLiveSource(_selectItem1);
                _metadataLiveSource.LiveModeStart = true;
                _metadataLiveSource.Init();
                _metadataLiveSource.LiveContentEvent += OnLiveContentEvent;
            }

            rs.Status = true;
        }
        catch (Exception ex)
        {
            log.Error(ex);
            rs.Status = false;
            rs.Message = ex.Message;
        }

        return rs;
    }

    public static void OnLiveContentEvent(MetadataLiveSource sender, MetadataLiveContent e)
    {
        if (e.Content == null) return;

        var metadataXml = e.Content.GetMetadataString();

        _ = Task.Run(async () =>
        {
            try
            {
                await SendMetadataAsync(metadataXml);
            }
            catch (Exception ex)
            {
                log.Error("Send metadata failed", ex);
            }
        });
    }

    public static async Task SendMetadataAsync(string metadata)
    {
        var url = ConfigurationManager.AppSettings["UrlCCTV"];

        var content = new StringContent(
            metadata,
            Encoding.UTF8,
            "application/xml"
        );

        var response = await _httpClient.PostAsync(url, content);

        response.EnsureSuccessStatusCode();
    }

   
}
