using System;
using MQTTnet.Extensions.ManagedClient;

namespace SimHub.HomeAssistant.MQTT.Config
{
    public class SensorConfig : BaseConfig
    {
        public override string Component => "sensor";
        public override object EmptyValue { get; }
        public override Type ValueType => typeof(string);

        public SensorConfig(ref BaseConfigDevice device, string name, string uniqueId, ref IManagedMqttClient managedMqttClient, string icon = null, string emptyValue = null) :
            base(ref device, name, uniqueId, ref managedMqttClient, icon)
        {
            EmptyValue = emptyValue;
            
            Init();
        }
    }
}