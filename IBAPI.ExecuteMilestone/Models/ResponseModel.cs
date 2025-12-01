

namespace IBAPI.ExecuteMilestone.Model
{
    public class ResponseModel
    {
        public bool Status { get; set; }
        public string Message { get; set; }
        public string UrlPath { get; set; }
    }
    public class ResponsePropertiesModel: ResponseModel
    {
        public double zoom { get; set; }
        public double pan { get; set; }
        public double tilt { get; set; }
    }
}