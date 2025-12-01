using IBAPI.ExecuteMilestone.Model;
using Microsoft.IdentityModel.Logging;
using System;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;
using System.Web.Http;

namespace MipSdkService.Controllers
{
    public class MipSdkController : ApiController
    {
        private readonly string _bucketName = ConfigurationManager.AppSettings["MinIO:BucketName"];

        [HttpPost]
        public async Task<IHttpActionResult> Export([FromBody] ExportVideoInfor param)
        {
            var rs = new ResponseModel { Status = false , Message = "Fail"}; 
            try
            {

                rs = await MilestoneServices.ExportCameraVideo(param);
                if (rs.Status)
                {
                    try
                    {
                        var result = await MinIOServices.BackgroundExportProcess(rs);
                        if (!string.IsNullOrEmpty(result.FilePath))
                        {
                            rs.Status = true;
                            rs.Message = "Success";
                            rs.UrlPath = "/" + _bucketName + "/" + result.FilePath;
                        }
                    }
                    catch (Exception ex)
                    {

                        rs.Status = false;
                        rs.Message = ex.Message;
                    }

                }
            }
            catch (Exception ex)
            {
                rs.Status = false;
                rs.Message = ex.Message;
            }

            return Ok(rs);
        }
    }
}
