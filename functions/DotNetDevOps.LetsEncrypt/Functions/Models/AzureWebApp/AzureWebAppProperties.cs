using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace DotNetDevOps.LetsEncrypt
{
    public class AzureWebAppProperties : TargetProperties
    {
        [Required]
        public string SubscriptionId { get; set; }
        [Required]
        public string ResourceGroupName { get; set; }
        [Required]
        public string SiteName { get; set; }
        public string SlotName { get; set; }
        public bool? UseIpBasedSsl { get; set; }
    }
}
