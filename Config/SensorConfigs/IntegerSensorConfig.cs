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

        public IntegerSensorConfig(ref BaseConfigDevice device, string name, string uniqueId, ref IManagedMqttClient managedMqttClient, string icon = null, int? emptyValue = null, string unitOfMeasurement = null) :
            base(ref device, name, uniqueId, ref managedMqttClient, icon)
        {
            EmptyValue = emptyValue;
            UnitOfMeasurement = unitOfMeasurement;

            Init();

            UpdateSensorState(emptyValue);
        }
    }
}