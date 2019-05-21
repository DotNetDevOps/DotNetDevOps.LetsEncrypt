using Newtonsoft.Json;

namespace DotNetDevOps.LetsEncrypt
{

    [JsonConverter(typeof(TargetConverter))]
    public class Target
    {
        public string Type { get; set; }
      
        public TargetProperties Properties { get; set; }
        internal string Hash { get; set; }
    }
}
