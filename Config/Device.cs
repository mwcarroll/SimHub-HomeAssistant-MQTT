using Newtonsoft.Json;

namespace SimHub.HomeAssistant.MQTT.Config
{
    public class Device
    {
        [JsonProperty("identifiers")]
        public string[] Identifiers { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("manufacturer")]
        public string Manufacturer => "github.com/mwcarroll";

        [JsonProperty("sw_version")]
        public string SoftwareVersion => "0.0.1";

        [JsonProperty("configuration_url")]
        public string ConfigurationUrl => "https://github.com/mwcarroll/SimHub-HomeAssistant-MQTT";

        [JsonProperty("model")]
        public string Model => $"SimHub {SimHubVersion}";

        [JsonIgnore]
        public string SimHubVersion { get; set; }
    }
}