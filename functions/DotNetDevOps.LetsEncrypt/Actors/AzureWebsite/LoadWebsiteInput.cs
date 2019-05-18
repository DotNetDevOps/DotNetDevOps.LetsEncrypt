namespace DotNetDevOps.LetsEncrypt
{
    public class LoadWebsiteInput
    {
        public string SubscriptionId { get; set; }
        public string ResourceGroupname { get; set; }
        public string SiteName { get; set; }
        public string SlotName { get; set; }
        public string[] Domains { get;  set; }
    }
}
