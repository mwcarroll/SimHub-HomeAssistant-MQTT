using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using IRacingReader;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using SimHub.HomeAssistant.MQTT.Properties;
using SimHub.HomeAssistant.MQTT.Settings;
using SimHub.Plugins;

namespace SimHub.HomeAssistant.MQTT
{
    [PluginAuthor("mwcarroll")]
    [PluginDescription("HomeAssistant focused MQTT publisher.")]
    [PluginName("Home Assistant MQTT Publisher")]
    // ReSharper disable once ClassNeverInstantiated.Global
    public class SimHubHomeAssistantMqttPlugin : IDataPlugin, IWPFSettingsV2
    {
        public SimHubHomeAssistantMQTTPluginSettings Settings;

        public SimHubHomeAssistantMQTTPluginUserSettings UserSettings { get; private set; }

        private MqttFactory _mqttFactory;
        private IManagedMqttClient _mqttClient;

        private int _ticksSinceLastInform;

        private SensorConfig _raceSessionTypeSensorConfig;
        private SensorConfig _raceSessionTimeSensorConfig;

        /// <summary>
        /// Instance of the current plugin manager
        /// </summary>
        public PluginManager PluginManager { get; set; }

        /// <summary>
        /// Gets the left menu icon. Icon must be 24x24 and compatible with black and white display.
        /// </summary>
        public ImageSource PictureIcon => this.ToIcon(Resources.sdkmenuicon);

        /// <summary>
        /// Gets a short plugin title to show in left menu. Return null if you want to use the title as defined in PluginName attribute.
        /// </summary>
        public string LeftMenuTitle => "HomeAssistant MQTT Publisher";

        /// <summary>
        /// Called one time per game data update, contains all normalized game data,
        /// raw data are intentionally "hidden" under a generic object type (A plugin SHOULD NOT USE IT)
        ///
        /// This method is on the critical path, it must execute as fast as possible and avoid throwing any error
        ///
        /// </summary>
        /// <param name="pluginManager"></param>
        /// <param name="data">Current game data, including current and previous data frame.</param>
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            int updateRate = pluginManager.GetPropertyValue("DataCorePlugin.DataUpdateFps") is int ? (int)pluginManager.GetPropertyValue("DataCorePlugin.DataUpdateFps") : 60;
            
            if (_ticksSinceLastInform > updateRate)
            {
                Logging.Current.Info($"Ticks since last inform at {_ticksSinceLastInform}...");

                _ticksSinceLastInform++;
                
                return;
            }
            
            if (!data.GameRunning || data.GameName != "IRacing")
            {
                Logging.Current.Info("Sending 'Unknown'...");
                
                Task.Run(async () => await _mqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                    .WithTopic(_raceSessionTypeSensorConfig.StateTopic)
                    .WithPayload(JsonConvert.SerializeObject(new { value = "Unknown" }))
                    .Build())
                );
                
                Task.Run(async () => await _mqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                    .WithTopic(_raceSessionTypeSensorConfig.StateTopic)
                    .WithPayload(JsonConvert.SerializeObject(new { value = 0 }))
                    .Build())
                );
            }
            else
            {
                DataSampleEx irData = data.NewData.GetRawDataObject() as DataSampleEx;
                
                string sessionType = irData?.SessionData.WeekendInfo.EventType ?? "Unknown";
                object sessionTime = pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.SessionTimeOfDay") ?? 0;
                
                Logging.Current.Debug($"Sending game value of \"{sessionType}\"...");
                
                Task.Run(async () => await _mqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                    .WithTopic(_raceSessionTypeSensorConfig.StateTopic)
                    .WithPayload(JsonConvert.SerializeObject(new { value = sessionType }))
                    .Build())
                );
                
                Task.Run(async () => await _mqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                    .WithTopic(_raceSessionTimeSensorConfig.StateTopic)
                    .WithPayload(JsonConvert.SerializeObject(new { value = sessionTime }))
                    .Build())
                );
            }
            
            Logging.Current.Debug("Resetting tick counter...");

            _ticksSinceLastInform = 0;
        }

        /// <summary>
        /// Called at plugin manager stop, close/dispose anything needed here !
        /// Plugins are rebuilt at game change
        /// </summary>
        /// <param name="pluginManager"></param>
        public void End(PluginManager pluginManager)
        {
            // Save settings
            this.SaveCommonSettings("GeneralSettings", Settings);
            this.SaveCommonSettings("UserSettings", UserSettings);
            
            _mqttClient.Dispose();
        }

        /// <summary>
        /// Returns the settings control, return null if no settings control is required
        /// </summary>
        /// <param name="pluginManager"></param>
        /// <returns></returns>
        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new SimHubHomeAssistantMqttPluginUi(this);
        }

        /// <summary>
        /// Called once after plugins startup
        /// Plugins are rebuilt at game change
        /// </summary>
        /// <param name="pluginManager"></param>
        public void Init(PluginManager pluginManager)
        {
            Logging.Current.Debug("Starting SimHub.HomeAssistant.MQTT");

            // Load settings
            Settings = this.ReadCommonSettings("GeneralSettings", () => new SimHubHomeAssistantMQTTPluginSettings());
            UserSettings = this.ReadCommonSettings("UserSettings", () => new SimHubHomeAssistantMQTTPluginUserSettings());

            _mqttFactory = new MqttFactory();

            CreateMqttClient();
            
            // create sensor config
            _raceSessionTypeSensorConfig = SensorConfig.CreateInstance("simhub/session/type", "SimHub Race Session Type", $"simhub-race-session-type-{UserSettings.UserId}");
            
            // build config inform message for home assistant
            MqttApplicationMessage raceTypeConfigMessage = new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/{_raceSessionTypeSensorConfig.Component}/{_raceSessionTypeSensorConfig.UniqueId}/config")
                .WithRetainFlag()
                .WithPayload(JsonConvert.SerializeObject(_raceSessionTypeSensorConfig))
                .Build();
            
            Logging.Current.Debug(JsonConvert.SerializeObject(_raceSessionTypeSensorConfig));

            // send inform to home assistant
            Task.Run(async () => await _mqttClient.EnqueueAsync(raceTypeConfigMessage)).Wait();
            
            
            // create sensor config
            _raceSessionTimeSensorConfig = SensorConfig.CreateInstance("simhub/session/time", "SimHub In-Sim Time", $"simhub-in-sim-time-{UserSettings.UserId}");
            
            // build config inform message for home assistant
            MqttApplicationMessage raceTimeConfigMessage = new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/{_raceSessionTimeSensorConfig.Component}/{_raceSessionTimeSensorConfig.UniqueId}/config")
                .WithRetainFlag()
                .WithPayload(JsonConvert.SerializeObject(_raceSessionTimeSensorConfig))
                .Build();

            // send inform to home assistant
            Task.Run(async () => await _mqttClient.EnqueueAsync(raceTimeConfigMessage)).Wait();
        }

        internal void CreateMqttClient()
        {
            IManagedMqttClient newMqttClient = _mqttFactory.CreateManagedMqttClient();

            MqttClientOptions mqttClientOptions = new MqttClientOptionsBuilder()
               .WithTcpServer(Settings.Server)
               .WithCredentials(Settings.Login, Settings.Password)
               .WithClientId($"SimHub-{Environment.MachineName}-{Guid.NewGuid().ToString()}")
               .Build();
            
            ManagedMqttClientOptions managedMqttClientOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(mqttClientOptions)
                .Build();

            newMqttClient.StartAsync(managedMqttClientOptions);

            IManagedMqttClient oldMqttClient = _mqttClient;

            _mqttClient = newMqttClient;

            oldMqttClient?.Dispose();
        }
    }

    public class SensorConfig
    {
        [JsonProperty("component")]
        public string Component = "sensor";
        
        [JsonProperty("state_topic")]
        public string StateTopic;
        
        [JsonProperty("value_template")]
        public string ValueTemplate = "{{ value_json.value }}";
        
        [JsonProperty("name")]
        public string Name;
        
        [JsonProperty("unique_id")]
        public string UniqueId;

        private SensorConfig(string stateTopic, string name, string uniqueId)
        {
            StateTopic = stateTopic;
            Name = name;
            UniqueId = uniqueId;
        }

        public static SensorConfig CreateInstance(string stateTopic, string name, string uniqueId)
        {
            return new SensorConfig(stateTopic, name, uniqueId);
        }
    }
}