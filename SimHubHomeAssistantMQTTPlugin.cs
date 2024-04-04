using System;
using System.Collections.Generic;
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

        private bool _iRacingNewSession;

        private MqttFactory _mqttFactory;
        private IManagedMqttClient _mqttClient;

        private readonly Dictionary<string, SensorConfig> _sensorConfigs = new Dictionary<string, SensorConfig>();

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
            if (!data.GameRunning || data.GameName.ToUpper() != "IRACING")
            {
                _iRacingNewSession = false;
                
                return;
            }
            
            if (!(data.NewData.GetRawDataObject() is DataSampleEx irData)) return;

            #region UpdateEveryTick
            
            string sessionStartDay = (string)irData.SessionDataDict["WeekendInfo.WeekendOptions.Date"];
            string sessionStartTime = (string)irData.SessionDataDict["WeekendInfo.WeekendOptions.TimeOfDay"];
            int earthRotationSpeedupFactor = int.Parse((string) irData.SessionDataDict["WeekendInfo.WeekendOptions.EarthRotationSpeedupFactor"]);
            double sessionTimePrecise = irData.Telemetry.SessionTime;
            
            DateTime inSimTime = DateTime.Parse($"{sessionStartDay} {sessionStartTime}").AddSeconds((int) (sessionTimePrecise * earthRotationSpeedupFactor));
            
            _sensorConfigs["SessionInSimTime"].UpdateSensorState(inSimTime, _mqttClient);
            
            #endregion
            

            #region UpdateWhenDifferent

            bool updateAll = !_iRacingNewSession;

            if (!updateAll)
            {
                DataSampleEx oldIrData = data.NewData.GetRawDataObject() as DataSampleEx;
                
                string oldSessionType = oldIrData?.SessionData.WeekendInfo.EventType ?? "Unknown";
                string newSessionType = irData.SessionData.WeekendInfo.EventType ?? "Unknown";
                if(!oldSessionType.Equals(newSessionType)){
                    _sensorConfigs["SessionType"].UpdateSensorState(newSessionType, _mqttClient);
                }
                
                bool oldIsSessionOfficial = ((string) oldIrData?.SessionDataDict["WeekendInfo.Official"] ?? "2").Equals("1");
                bool newIsSessionOfficial = ((string) irData.SessionDataDict["WeekendInfo.Official"]).Equals("1");
                if (!oldIsSessionOfficial.Equals(newIsSessionOfficial))
                {
                    _sensorConfigs["SessionIsOfficial"].UpdateSensorState(newIsSessionOfficial, _mqttClient);
                }

                int oldSessionLeagueId = int.Parse((string)oldIrData?.SessionDataDict["WeekendInfo.LeagueID"] ?? "0");
                int newSessionLeagueId = int.Parse((string)irData.SessionDataDict["WeekendInfo.LeagueID"]);
                if (!oldSessionLeagueId.Equals(newSessionLeagueId))
                {
                    _sensorConfigs["SessionLeagueId"].UpdateSensorState(newSessionLeagueId, _mqttClient);
                }

                string oldTrackAltitude = ((string)oldIrData?.SessionDataDict["WeekendInfo.TrackAltitude"] ?? "0").Replace(" m", "");
                string newTrackAltitude = ((string)irData.SessionDataDict["WeekendInfo.TrackAltitude"]).Replace(" m", "");
                if (!oldTrackAltitude.Equals(newTrackAltitude))
                {
                    _sensorConfigs["TrackInfoAltitude"].UpdateSensorState(newTrackAltitude, _mqttClient);
                }

                string oldTrackLatitude = ((string)oldIrData?.SessionDataDict["WeekendInfo.TrackLatitude"] ?? "0").Replace(" m", "");
                string newTrackLatitude = ((string)irData.SessionDataDict["WeekendInfo.TrackLatitude"]).Replace(" m", "");
                if (!oldTrackLatitude.Equals(newTrackLatitude))
                {
                    _sensorConfigs["TrackInfoLatitude"].UpdateSensorState(newTrackLatitude, _mqttClient);
                }

                string oldTrackLongitude = ((string)oldIrData?.SessionDataDict["WeekendInfo.TrackLongitude"] ?? "0").Replace(" m", "");
                string newTrackLongitude = ((string)irData.SessionDataDict["WeekendInfo.TrackLongitude"]).Replace(" m", "");
                if (!oldTrackLongitude.Equals(newTrackLongitude))
                {
                    _sensorConfigs["TrackInfoLongitude"].UpdateSensorState(newTrackLongitude, _mqttClient);
                }

                _iRacingNewSession = false;
            }
            else
            {
                string newSessionType = irData.SessionData.WeekendInfo.EventType ?? "Unknown";
                bool newIsSessionOfficial = ((string) irData.SessionDataDict["WeekendInfo.Official"]).Equals("1");
                int newSessionLeagueId = int.Parse((string)irData.SessionDataDict["WeekendInfo.LeagueID"]);
                
                string newTrackAltitude = ((string)irData.SessionDataDict["WeekendInfo.TrackAltitude"]).Replace(" m", "");
                string newTrackLatitude = ((string)irData.SessionDataDict["WeekendInfo.TrackLatitude"]).Replace(" m", "");
                string newTrackLongitude = ((string)irData.SessionDataDict["WeekendInfo.TrackLongitude"]).Replace(" m", "");
                
                _sensorConfigs["SessionType"].UpdateSensorState(newSessionType, _mqttClient);
                _sensorConfigs["SessionIsOfficial"].UpdateSensorState(newIsSessionOfficial, _mqttClient);
                _sensorConfigs["SessionLeagueId"].UpdateSensorState(newSessionLeagueId, _mqttClient);

                _sensorConfigs["TrackInfoAltitude"].UpdateSensorState(newTrackAltitude, _mqttClient);
                _sensorConfigs["TrackInfoLatitude"].UpdateSensorState(newTrackLatitude, _mqttClient);
                _sensorConfigs["TrackInfoLongitude"].UpdateSensorState(newTrackLongitude, _mqttClient);
            }
            
            #endregion
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

            foreach (KeyValuePair<string, SensorConfig> kvp in _sensorConfigs)
            {
                kvp.Value.UpdateSensorState(null, _mqttClient);
            }
            
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

            CreateMqttClient();
            
            // create sensor configs - auto publish for home assistant visibility
            _sensorConfigs.Add("SessionType", SensorConfig.CreateInstance("simhub/session/type", "SimHub Race Session Type", $"simhub-race-session-type-{UserSettings.UserId}", _mqttClient));
            _sensorConfigs.Add("SessionInSimTime", SensorConfig.CreateInstance("simhub/session/datetime", "SimHub Race Session In-Sim DateTime", $"simhub-race-session-in-sim-datetime-{UserSettings.UserId}", _mqttClient));
            _sensorConfigs.Add("SessionIsOfficial", SensorConfig.CreateInstance("simhub/session/is-official", "SimHub Race Session Is Official", $"simhub-race-session-is-session-official-{UserSettings.UserId}", _mqttClient));
            _sensorConfigs.Add("SessionLeagueId", SensorConfig.CreateInstance("simhub/session/leagueId", "SimHub Race Session League Id", $"simhub-race-session-league-id-{UserSettings.UserId}", _mqttClient));
            
            _sensorConfigs.Add("TrackInfoAltitude", SensorConfig.CreateInstance("simhub/track/altitude", "SimHub Track Altitude", $"simhub-track-altitude-{UserSettings.UserId}", _mqttClient));
            _sensorConfigs.Add("TrackInfoLatitude", SensorConfig.CreateInstance("simhub/track/latitude", "SimHub Track Latitude", $"simhub-track-latitude-{UserSettings.UserId}", _mqttClient));
            _sensorConfigs.Add("TrackInfoLongitude", SensorConfig.CreateInstance("simhub/track/longitude", "SimHub Track Longitude", $"simhub-track-longitude-{UserSettings.UserId}", _mqttClient));
            
            // initialize values as "none"
            foreach (KeyValuePair<string, SensorConfig> kvp in _sensorConfigs)
            {
                kvp.Value.UpdateSensorState(null, _mqttClient);
            }
        }

        internal void CreateMqttClient(bool disposeOldMqttClient = true)
        {
            _mqttFactory = new MqttFactory();
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

            if(disposeOldMqttClient) oldMqttClient?.Dispose();
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

        public static SensorConfig CreateInstance(string stateTopic, string name, string uniqueId, IManagedMqttClient mqttClient)
        {
            SensorConfig sensorConfig = new SensorConfig(stateTopic, name, uniqueId);
            
            MqttApplicationMessage raceTimeConfigMessage = new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/{sensorConfig.Component}/{sensorConfig.UniqueId}/config")
                .WithRetainFlag()
                .WithPayload(JsonConvert.SerializeObject(sensorConfig))
                .Build();

            // send inform to home assistant
            Task.Run(async () => await mqttClient.EnqueueAsync(raceTimeConfigMessage)).Wait();

            return sensorConfig;
        }
        
        public void UpdateSensorState(object newState, IManagedMqttClient mqttClient)
        {
            Task.Run(async () => await mqttClient.EnqueueAsync(new MqttApplicationMessageBuilder()
                .WithTopic(this.StateTopic)
                .WithPayload(JsonConvert.SerializeObject(new { value = newState }))
                .Build())
            );
        }
    }
}