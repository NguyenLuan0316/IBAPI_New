using IBAPI.ExecuteMilestone.Model;
using IBAPI.MetadataMilestone.Model;
using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Web.Http;

namespace MipSdkService.Controllers
{
    public class MetadataController : ApiController
    {
        private readonly string _bucketName = ConfigurationManager.AppSettings["MinIO:BucketName"];

        [HttpPost]
        public IHttpActionResult RegisterReceiveMetadata([FromBody]MetadataInput param)
        {
            var rs = new ResponseModel { Status = false , Message = "Fail"}; 
            try
            {
                rs = MilestoneServices.GetMetadataLiveViewer(param);
                if (rs.Status)
                {
                    try
                    {
                        rs.Status = true;
                        rs.Message = "Success";
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
