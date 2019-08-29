using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;



namespace DotNetDevOps.LetsEncrypt
{
    public class AuthorizeOrderOrchestratorInput
    {
        public string OrderLocation { get; set; }
        public EntityId EntityId { get; set; }
        public string RequestMonitorInstanceId { get;  set; }
        public bool UseDns01Authorization { get; set; }
        public string SignerEmail { get;  set; }
    }
}
