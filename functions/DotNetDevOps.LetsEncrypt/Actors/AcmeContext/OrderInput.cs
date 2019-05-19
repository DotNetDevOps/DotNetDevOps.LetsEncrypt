using Microsoft.Azure.WebJobs.Hosting;



namespace DotNetDevOps.LetsEncrypt
{
    public class OrderInput
    {
        public string[] Domains { get; set; }
        public string MonitorInstanceId { get; set; }
        public bool UseDns01Authorization { get;  set; }
    }
}
