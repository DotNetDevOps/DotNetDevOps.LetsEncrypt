using Newtonsoft.Json;

namespace DotNetDevOps.LetsEncrypt
{

    [JsonConverter(typeof(TargetConverter))]
    public class Target
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("properties")]
        public TargetProperties Properties { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }
    }
}
