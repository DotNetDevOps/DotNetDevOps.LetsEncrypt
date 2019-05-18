using System;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using Certes.Acme;
using Microsoft.Azure.WebJobs.Hosting;
using Certes;

namespace DotNetDevOps.LetsEncrypt
{
    
    public class AddCertificateRequest
    {
        [Required] 
        public string SignerEmail { get; set; }
        [Required]
        public string SubscriptionId { get; set; }
        [Required]
        public string ResourceGroupName { get; set; }
        [Required]
        public string SiteName { get; set; }
        public string SlotName { get; set; }
        [Required]
        public string[] Domains { get; set; }
        public bool? UseIpBasedSsl { get; set; }

        public Uri LetsEncryptEndpoint { get; set; } = WellKnownServers.LetsEncryptV2;
        public CsrInfo CsrInfo { get; set; }
    }
}
