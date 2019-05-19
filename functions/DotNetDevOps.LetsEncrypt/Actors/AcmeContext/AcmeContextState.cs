using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Hosting;



namespace DotNetDevOps.LetsEncrypt
{
    public class AcmeContextState
    {
        public string SignerEmail { get; set; }
        public string Pem { get;  set; }
        public Dictionary<string, string> Orders { get; set; } = new Dictionary<string, string>();
        
        public Uri LetsEncryptEndpoint { get; set; }
    }
}
