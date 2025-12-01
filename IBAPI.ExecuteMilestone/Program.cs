using System;
using System.Configuration;
using System.Web.Http;
using System.Web.Http.SelfHost;

namespace IBAPI.ExecuteMilestone
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
        }
    }
}
