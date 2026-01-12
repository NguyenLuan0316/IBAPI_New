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
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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

        [HttpPost]
        public IHttpActionResult RemoveStreamMetadata([FromBody] MetadataInput param)
        {
            var rs = new ResponseModel { Status = false, Message = "Fail" };
            try
            {
               rs = MilestoneServices.StopMetadata(param.MetadataId);
               log.ErrorFormat("ExportCameraVideo lỗi: " + rs.Message);
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
