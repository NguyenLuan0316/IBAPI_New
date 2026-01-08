using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.SelfHost;

namespace IBAPI.MetadataMilestone
{
    internal class Program
    {
        static void Main(string[] args)
        {

            string seftUrl = ConfigurationManager.AppSettings["SelfUrl"];

            var config = new HttpSelfHostConfiguration(seftUrl);

            config.Routes.MapHttpRoute(
                name: "API Default",
                routeTemplate: "api/{controller}/{action}"
            );

            using (var server = new HttpSelfHostServer(config))
            {
                server.OpenAsync().Wait();
                Console.WriteLine($"MIP SDK Service is running on {seftUrl} ...");
                Console.ReadLine();
            }

            //Khởi động worker
            MilestoneServices.StartSendWorker();
            ServiceStart.RegisterReceiveMetadataStart();

        }
    }
}
