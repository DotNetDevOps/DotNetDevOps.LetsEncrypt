using System;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using Certes.Acme;
using Microsoft.Azure.WebJobs.Hosting;
using Certes;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DotNetDevOps.LetsEncrypt
{
    public class TargetProperties
    {

    }
    public class Target
    {
        public string Type { get; set; }
      
        public TargetProperties Properties { get; set; }
        internal string PropertiesHash { get; set; }
    }
    public class FileSystemProperties : TargetProperties
    {
        public string Path { get; set; }
    }
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
    public class TargetConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jobj = JObject.ReadFrom(reader);
            var type = jobj.SelectToken("$.type").ToString();
            var props = jobj.SelectToken("$.properties");
            switch (type)
            {
                case "FileSystem":
                    return new Target { Type = type, Properties = props.ToObject<FileSystemProperties>(), PropertiesHash=props.ToString().ToMD5Hash() };
                case "AzureWebApp":
                    return new Target { Type = type, Properties = props.ToObject<AzureWebAppProperties>(), PropertiesHash = props.ToString().ToMD5Hash() };

            }
            return new Target { Type = type };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
    public class AddCertificateRequest
    {
        [Required] 
        public string SignerEmail { get; set; }
       
        [Required]
        public string[] Domains { get; set; }


        public Uri LetsEncryptEndpoint { get; set; } = WellKnownServers.LetsEncryptV2;
        public CsrInfo CsrInfo { get; set; }

        [JsonConverter(typeof(TargetConverter))]
        public Target Target { get; set; }
    }
}
