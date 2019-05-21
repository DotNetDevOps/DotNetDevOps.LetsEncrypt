namespace DotNetDevOps.LetsEncrypt
{
    public class Target
    {
        public string Type { get; set; }
      
        public TargetProperties Properties { get; set; }
        internal string PropertiesHash { get; set; }
    }
}
