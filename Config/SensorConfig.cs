using System;
using MQTTnet.Client;

namespace SimHub.HomeAssistant.MQTT.Config
{
    public class SensorConfig : BaseConfig
    {
        public override string Component => "sensor";
        public override object EmptyValue { get; }
        public override Type ValueType => typeof(string);

        public SensorConfig(ref BaseConfigDevice device, string name, string uniqueId, ref IMqttClient mqttClient, string icon = null, string emptyValue = null) :
            base(ref device, name, uniqueId, ref mqttClient, icon)
        {
            EmptyValue = emptyValue;
            
            Init();
        }
    }
}