using System;
using Microsoft.Azure.WebJobs.Hosting;



namespace DotNetDevOps.LetsEncrypt
{
    public class AcmeContextInitializeInput
    {
        public string SignerEmail { get; set; }
        public Uri LetsEncryptEndpoint { get; set; }
    }
}
