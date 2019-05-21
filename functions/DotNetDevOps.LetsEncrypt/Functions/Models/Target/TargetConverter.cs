using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotNetDevOps.LetsEncrypt
{
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
                case "AzureBlob":
                    return new Target { Type = type, Properties = props.ToObject<AzureBlobProperties>(), PropertiesHash = props.ToString().ToMD5Hash() };

            }
            return new Target { Type = type };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
