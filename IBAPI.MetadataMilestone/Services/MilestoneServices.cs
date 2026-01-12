using IBAPI.ExecuteMilestone.Model;
using IBAPI.MetadataMilestone.Model;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Channels;
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

    private static readonly object _loginLock = new object();
    private static bool _isInitialized;
    private static readonly HttpClient _httpClient =
        new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

    private static FQID _playbackFQID;
    private static readonly object _lock = new object();
    private static readonly Dictionary<Guid, MetadataLiveSource> _metadataSources = new Dictionary<Guid, MetadataLiveSource>();

    private static readonly Channel<string> _channel =
    Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    private static readonly BlockingCollection<string> _queue =
    new BlockingCollection<string>(new ConcurrentQueue<string>(), 500);

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

            lock (_lock)
            {
                if (_metadataSources.TryGetValue(param.MetadataId, out var existingSource))
                {
                    rs.Status = true;
                    return rs;
                }

                var item = VideoOS.Platform.Configuration.Instance.GetItem(param.MetadataId, Kind.Metadata);

                if (item == null)
                {
                    rs.Status = false;
                    rs.Message = "Metadata item not found";
                    return rs;
                }

                var source = new MetadataLiveSource(item);
                source.LiveModeStart = true;
                source.Init();
                source.LiveContentEvent += OnLiveContentEvent;

                _metadataSources[param.MetadataId] = source;
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
        if (e?.Content == null) return;
        _queue.TryAdd(e.Content.GetMetadataString());
    }

    public static Task StartSendWorker()
    {
        return Task.Run(async () =>
        {
            foreach (var msg in _queue.GetConsumingEnumerable())
            {
                try
                {
                    await SendMetadataAsync(msg);
                }
                catch (Exception ex)
                {
                    log.Error("Send metadata failed", ex);
                }
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

    public static ResponseModel StopMetadata(Guid metadataId)
    {
        var rs = new ResponseModel { Status = false, Message = "Fail" };

        try
        {
            lock (_lock)
            {
                if (_metadataSources.TryGetValue(metadataId, out var source))
                {
                    source.LiveContentEvent -= OnLiveContentEvent;
                    source.Close();
                    _metadataSources.Remove(metadataId);

                    rs.Status = true;
                    rs.Message = "Success";
                    return rs;
                }
                return rs;
            }
        }
        catch (Exception ex)
        {
            rs.Status = false;
            rs.Message = ex.Message.ToString();
            return rs;
        }
        
    }
    public static void StopAll()
    {
        lock (_lock)
        {
            foreach (var source in _metadataSources.Values)
            {
                source.LiveContentEvent -= OnLiveContentEvent;
                source.Close();
            }

            _metadataSources.Clear();
        }
    }
}
