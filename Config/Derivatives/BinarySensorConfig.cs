using System;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;

namespace SimHub.HomeAssistant.MQTT.Config.Derivatives
{
    public class BinarySensorConfig : DiscoveryConfig
    {
        [JsonProperty("device_class")]
        public string DeviceClass { get; private set; }

        [JsonProperty("payload_on")]
        public string PayloadOn { get; private set; }

        [JsonProperty("payload_off")]
        public string PayloadOff { get; private set; }

        public override string Component => "binary_sensor";
        public override object EmptyValue { get; }
        public override Type ValueType => typeof(bool?);

        public BinarySensorConfig(Device device, string name, string uniqueId, IManagedMqttClient mqttClient, string icon = null, bool? emptyValue = null, string deviceClass = null, string payloadOn = null, string payloadOff = null) :
            base(device, name, uniqueId, mqttClient, icon)
        {
            EmptyValue = emptyValue;
            DeviceClass = deviceClass;
            PayloadOn = payloadOn ?? "ON";
            PayloadOff = payloadOff ?? "OFF";
            
            Init();

            UpdateSensorState(emptyValue);
        }
    }
}