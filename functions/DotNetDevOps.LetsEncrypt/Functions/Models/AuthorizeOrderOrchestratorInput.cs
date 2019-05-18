using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;



namespace DotNetDevOps.LetsEncrypt
{
    public class AuthorizeOrderOrchestratorInput
    {
        public string OrderLocation { get; set; }
        public EntityId EntityId { get; set; }
        public string OrderId { get; set; }
        public Uri LetsEncryptEndpoint { get;  set; }
        public string RequestMonitorInstanceId { get;  set; }
    }
}
