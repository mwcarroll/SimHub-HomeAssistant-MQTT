using System;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using SimHub.HomeAssistant.MQTT.Config.Derivatives;

namespace SimHub.HomeAssistant.MQTT.Config
{
    public abstract class DiscoveryConfig
    {
        [JsonProperty("device")]
        public Device Device { get; private set; }

        [JsonIgnore]
        public abstract string Component { get; }
        
        [JsonIgnore]
        public abstract object EmptyValue { get; }
        
        [JsonIgnore]
        public abstract Type ValueType { get; }

        [JsonProperty("retain")]
        public bool Retain => true;

        [JsonIgnore]
        public string ConfigTopic => $"homeassistant/{Component}/{UniqueId}/config";

        [JsonProperty("state_topic")]
        public string StateTopic => $"homeassistant/{Component}/{UniqueId}/state";


        [JsonProperty("value_template")]
        public string ValueTemplate = "{{ value_json.state }}";

        [JsonProperty("name")]
        public string Name { get; private set; }

        [JsonProperty("unique_id")]
        public string UniqueId
        {
            get { return $"{Device.Identifiers[0]}-{Component}-{uniqueId}"; }
            private set { uniqueId = value ; }
        }

        [JsonProperty("icon")]
        public string Icon { get; private set; }

        private string uniqueId;

        [JsonIgnore]
        public IManagedMqttClient ManagedMqttClient { get; private set; }

        protected DiscoveryConfig(Device device, string name, string uniqueId, IManagedMqttClient managedMqttClient, string icon = null)
        {
            Device = device;
            Name = name;
            UniqueId = uniqueId;
            ManagedMqttClient = managedMqttClient;

            Icon = icon;
        }

        protected void Init()
        {
            MqttApplicationMessage mqttApplicationMessage = new MqttApplicationMessageBuilder()
               .WithTopic(ConfigTopic)
               .WithPayload(JsonConvert.SerializeObject(this, Formatting.Indented))
               .Build();

            Logging.Current.Info(ConfigTopic);
            Logging.Current.Info(JsonConvert.SerializeObject(this, Formatting.Indented));

            // send inform to home assistant
            Task.Run(async () => await ManagedMqttClient.EnqueueAsync(mqttApplicationMessage)).Wait();
        }

        public void UpdateSensorState(object newState)
        {
            if(!ManagedMqttClient.IsStarted || !ManagedMqttClient.IsConnected)
            {
                // TODO: inform the user that the MQTT connection is not established
                return;
            }

            if (newState.GetType() == typeof(bool) || newState.GetType() == typeof(bool?))
            {
                newState = (bool)newState ? ((BinarySensorConfig)this).PayloadOn : ((BinarySensorConfig)this).PayloadOff;
            }

            Task.Run(async () => await ManagedMqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                .WithTopic(StateTopic)
                .WithPayload(JsonConvert.SerializeObject(new { state = newState }))
                .Build())
            );
        }
    }
}