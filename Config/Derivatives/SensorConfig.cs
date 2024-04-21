using System;
using MQTTnet.Extensions.ManagedClient;

namespace SimHub.HomeAssistant.MQTT.Config.Derivatives
{
    public class SensorConfig : DiscoveryConfig
    {
        public override string Component => "sensor";
        public override object EmptyValue { get; }
        public override Type ValueType => typeof(string);

        public SensorConfig(Device device, string name, string uniqueId, IManagedMqttClient mqttClient, string icon = null, string emptyValue = null) :
            base(device, name, uniqueId, mqttClient, icon)
        {
            EmptyValue = emptyValue;
            
            Init();
        }
    }
}