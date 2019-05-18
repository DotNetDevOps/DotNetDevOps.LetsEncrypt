using Microsoft.Azure.WebJobs.Hosting;


namespace DotNetDevOps.LetsEncrypt
{
    public class UpdateDNSInput
    {
        public string DnsTxt { get;  set; }
        public string OrderId { get;  set; }
        public string Location { get;  set; }
        public string MonitorInstanceId { get;  set; }
        public string Name { get;  set; }
    }
}
