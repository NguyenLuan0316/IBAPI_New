using IBAPI.ExecuteMilestone.Common;
using IBAPI.ExecuteMilestone.Model;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using VideoOS.Platform;
using VideoOS.Platform.Data;

public static class MinIOServices
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    public static async Task<FileMinIOResponseDto> BackgroundExportProcess(ResponseModel param)
    {
        var minio = new MinioManager();
        var response = await minio.UploadAsync(param.UrlPath);
        return response;
    }
}
