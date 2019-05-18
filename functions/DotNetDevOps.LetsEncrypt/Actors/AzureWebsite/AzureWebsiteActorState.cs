using Microsoft.Azure.Management.AppService.Fluent.Models;

namespace DotNetDevOps.LetsEncrypt
{
    public class AzureWebsiteActorState
    {
        public SiteInner Site { get; set; }
        public string SubscriptionId { get; set; }
    }
}
