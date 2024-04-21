using System;
using MQTTnet.Client;
using Newtonsoft.Json;

namespace SimHub.HomeAssistant.MQTT.Config
{
    public class BinarySensorConfig : BaseConfig
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

        public BinarySensorConfig(ref BaseConfigDevice device, string name, string uniqueId, ref IMqttClient mqttClient, string icon = null, bool? emptyValue = null, string deviceClass = null, string payloadOn = null, string payloadOff = null) :
            base(ref device, name, uniqueId, ref mqttClient, icon)
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