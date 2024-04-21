using System;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;

namespace SimHub.HomeAssistant.MQTT.Config
{
    public class BaseConfigAvailability
    {
        [JsonProperty("topic")]
        public string Topic { get; set; }
    }

    public abstract class BaseConfig
    {
        [JsonProperty("device")]
        public BaseConfigDevice Device { get; private set; }

        [JsonProperty("availability")]
        public BaseConfigAvailability Availabilty => new BaseConfigAvailability() { Topic = $"homeassistant/{Component}/{UniqueId}/state" };

        [JsonIgnore]
        public abstract string Component { get; }

        [JsonIgnore]
        public abstract object EmptyValue { get; }

        [JsonIgnore]
        public abstract Type ValueType { get; }

        [JsonIgnore]
        public string ConfigTopic => $"homeassistant/{Component}/{UniqueId}/config";

        [JsonProperty("state_topic")]
        public string StateTopic => $"homeassistant/{Component}/{UniqueId}/state";

        [JsonProperty("value_template")]
        public string ValueTemplate = "{{ value_json.state }}";

        [JsonProperty("name")]
        public string Name { get; private set; }

        private string uniqueId;

        [JsonProperty("unique_id")]
        public string UniqueId
        {
            get { return $"{Device.Identifiers[0]}-{Component}-{uniqueId}"; }
            private set { uniqueId = value; }
        }

        [JsonProperty("icon")]
        public string Icon { get; private set; }

        [JsonIgnore]
        public IManagedMqttClient ManagedMqttClient { get; private set; }

        protected BaseConfig(ref BaseConfigDevice device, string name, string uniqueId, ref IManagedMqttClient managedMqttClient, string icon = null)
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

            Logging.Current.Info($"Informed: {ConfigTopic}");

            // send inform to home assistant
            ManagedMqttClient.EnqueueAsync(mqttApplicationMessage).Wait();
            UpdateSensorAvailability(true);
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

            Task.Run(async () => await ManagedMqttClient.EnqueueAsync(
                    new MqttApplicationMessageBuilder()
                        .WithTopic(StateTopic)
                        .WithPayload(JsonConvert.SerializeObject(new { state = newState }))
                        .Build()
                )
            );
        }

        public void UpdateSensorAvailability(bool availability)
        {
            ManagedMqttClient.EnqueueAsync(
                new MqttApplicationMessageBuilder()
                    .WithTopic(Availabilty.Topic)
                    .WithPayload(availability ? "online" : "offline")
                    .Build()
            );
        }
    }
}