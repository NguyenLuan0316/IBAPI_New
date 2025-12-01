using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace IBAPI.ExecuteMilestone.Model
{
    public class ExportVideoInfor
    {
        public string DestPathVideo { get; set; }

        public Guid CameraId { get; set; }

        public string StartTime { get; set; }

        public string EndTime { get; set; }

        public string FileOutType { get; set; }

        public string FileName { get; set; }
    }
}