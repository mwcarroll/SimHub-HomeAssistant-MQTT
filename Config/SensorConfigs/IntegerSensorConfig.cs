using System;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;

namespace SimHub.HomeAssistant.MQTT.Config.SensorConfigs
{
    public class IntegerSensorConfig : BaseConfig
    {
        [JsonProperty("unit_of_measurement")]
        public string UnitOfMeasurement { get; set; }

        public override string Component => "sensor";
        public override object EmptyValue { get; }
        public override Type ValueType => typeof(int?);

        public IntegerSensorConfig(BaseConfigDevice device, string name, string uniqueId, IManagedMqttClient mqttClient, string icon = null, int? emptyValue = null, string unitOfMeasurement = null) :
            base(device, name, uniqueId, mqttClient, icon)
        {
            EmptyValue = emptyValue;
            UnitOfMeasurement = unitOfMeasurement;

            Init();

            UpdateSensorState(emptyValue);
        }
    }
}