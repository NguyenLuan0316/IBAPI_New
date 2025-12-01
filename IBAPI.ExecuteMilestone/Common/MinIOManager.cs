using Amazon.S3;
using Amazon.S3.Model;
using IBAPI.ExecuteMilestone.Model;
using Microsoft.IdentityModel.Logging;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace IBAPI.ExecuteMilestone.Common
{
    public class MinioManager 
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _bucketName;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public MinioManager()
        {
            var endpoint = ConfigurationManager.AppSettings["MinIO:Endpoint"];
            var accessKey = ConfigurationManager.AppSettings["MinIO:AccessKey"];
            var secretKey = ConfigurationManager.AppSettings["MinIO:SecretKey"];
            _bucketName = ConfigurationManager.AppSettings["MinIO:BucketName"];
            bool useSsl = bool.TryParse(ConfigurationManager.AppSettings["MinIO:UseSSL"], out var ssl) && ssl;

            var s3Config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,     
                UseHttp = !useSsl,        
            };

            _s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);
        }

        public async Task<FileMinIOResponseDto> UploadAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                log.Error("Không tìm thấy file:" + filePath);
                throw new FileNotFoundException("Không tìm thấy file", filePath);

            }

            var key = $"{Guid.NewGuid()}{Path.GetExtension(filePath)}";
            var contentType = MimeMapping.GetMimeMapping(filePath);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = key,
                        InputStream = stream,
                        ContentType = contentType
                    };

                    await _s3Client.PutObjectAsync(putRequest);
                }

                return new FileMinIOResponseDto
                {
                    Key = Path.GetFileNameWithoutExtension(key),
                    Extension = Path.GetExtension(filePath),
                    FilePath = key,
                    Size = new FileInfo(filePath).Length,
                    OriginalFilename = Path.GetFileName(filePath)
                };
            }
            catch (Exception ex)
            {
                log.Error("Lỗi khi upload file lên MinIO", ex);

                throw new Exception("Lỗi khi upload file lên MinIO", ex);
            }
        }

        public string GetPresignedUrl(string key, int expireMinutes = 5)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Expires = DateTime.UtcNow.AddMinutes(expireMinutes)
            };

            var url = _s3Client.GetPreSignedURL(request);

            // Nếu endpoint cấu hình http mà SDK trả về https → sửa lại cho khớp
            if (_s3Client.Config.ServiceURL?.StartsWith("http://") == true && url.StartsWith("https://"))
            {
                url = "http://" + url.Substring("https://".Length);
            }

            return url;
        }
    }
}
