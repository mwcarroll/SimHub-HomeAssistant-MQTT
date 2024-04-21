using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using IRacingReader;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using SimHub.HomeAssistant.MQTT.Config;
using SimHub.HomeAssistant.MQTT.Config.SensorConfigs;
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

        private Dictionary<string, BaseConfig> _configs = new Dictionary<string, BaseConfig>();

        /// <summary>
        /// Instance of the current plugin manager
        /// </summary>
        public PluginManager PluginManager { get; set; }

        /// <summary>
        /// Gets the left menu icon. Icon must be 24x24 and compatible with black and white display.
        /// </summary>
        public ImageSource PictureIcon => this.ToIcon(Resources.SH_HA_MQTT_MenuIcon);

        /// <summary>
        /// Gets a short plugin title to show in left menu. Return null if you want to use the title as defined in PluginName attribute.
        /// </summary>
        public string LeftMenuTitle => "HomeAssistant MQTT Publisher";

        Int64 counter = 0;

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
            // TODO: remove later... just testing sending "devices"
            foreach (KeyValuePair<string, BaseConfig> kvp in _configs)
            {
                if(kvp.Value.GetType() == typeof(BinarySensorConfig))
                {
                    kvp.Value.UpdateSensorState(counter % 10 == 0 ? "ON" : "OFF");
                }
                else
                {
                    kvp.Value.UpdateSensorState(counter);
                }

                counter++;
            }

            if (!data.GameRunning || data.GameName.ToUpper() != "IRACING")
            {
                _iRacingNewSession = false;

                return;
            }

            if (!(data.NewData.GetRawDataObject() is DataSampleEx irData)) return;

            #region UpdateEveryTick

            string sessionStartDay = (string)irData.SessionDataDict["WeekendInfo.WeekendOptions.Date"];
            string sessionStartTime = (string)irData.SessionDataDict["WeekendInfo.WeekendOptions.TimeOfDay"];
            int earthRotationSpeedupFactor = int.Parse((string)irData.SessionDataDict["WeekendInfo.WeekendOptions.EarthRotationSpeedupFactor"]);
            double sessionTimePrecise = irData.Telemetry.SessionTime;

            DateTime inSimTime = DateTime.Parse($"{sessionStartDay} {sessionStartTime}").AddSeconds((int)(sessionTimePrecise * earthRotationSpeedupFactor));

            _configs["SessionInSimTime"].UpdateSensorState(inSimTime);

            #endregion

            #region UpdateWhenDifferent

            bool updateAll = !_iRacingNewSession;

            if (!updateAll)
            {
                DataSampleEx oldIrData = data.OldData.GetRawDataObject() as DataSampleEx;

                bool oldDriverIsInCar = oldIrData.Telemetry.DriverMarker;
                bool newDriverIsInCar = irData.Telemetry.DriverMarker;
                if (!oldDriverIsInCar.Equals(newDriverIsInCar))
                {
                    _configs["DriverInfoIsInCar"].UpdateSensorState(newDriverIsInCar);
                }

                //GameRawData.Telemetry.IsReplayPlaying
                bool oldDriverIsInReplay = oldIrData.Telemetry.IsReplayPlaying;
                bool newDriverIsInReplay = irData.Telemetry.IsReplayPlaying;
                if (!oldDriverIsInReplay.Equals(newDriverIsInReplay))
                {
                    _configs["DriverInfoIsInReplay"].UpdateSensorState(newDriverIsInReplay);
                }

                string oldSessionType = oldIrData?.SessionData.WeekendInfo.EventType ?? "Unknown";
                string newSessionType = irData.SessionData.WeekendInfo.EventType ?? "Unknown";
                if (!oldSessionType.Equals(newSessionType))
                {
                    _configs["SessionType"].UpdateSensorState(newSessionType);
                }

                bool oldIsSessionOfficial = ((string)oldIrData?.SessionDataDict["WeekendInfo.Official"] ?? "2").Equals("1");
                bool newIsSessionOfficial = ((string)irData.SessionDataDict["WeekendInfo.Official"]).Equals("1");
                if (!oldIsSessionOfficial.Equals(newIsSessionOfficial))
                {
                    _configs["SessionIsOfficial"].UpdateSensorState(newIsSessionOfficial);
                }

                int oldSessionLeagueId = int.Parse((string)oldIrData?.SessionDataDict["WeekendInfo.LeagueID"] ?? "0");
                int newSessionLeagueId = int.Parse((string)irData.SessionDataDict["WeekendInfo.LeagueID"]);
                if (!oldSessionLeagueId.Equals(newSessionLeagueId))
                {
                    _configs["SessionLeagueId"].UpdateSensorState(newSessionLeagueId);
                }

                string oldTrackAltitude = ((string)oldIrData?.SessionDataDict["WeekendInfo.TrackAltitude"] ?? "0").Replace(" m", "");
                string newTrackAltitude = ((string)irData.SessionDataDict["WeekendInfo.TrackAltitude"]).Replace(" m", "");
                if (!oldTrackAltitude.Equals(newTrackAltitude))
                {
                    _configs["TrackInfoAltitude"].UpdateSensorState(newTrackAltitude);
                }

                string oldTrackLatitude = ((string)oldIrData?.SessionDataDict["WeekendInfo.TrackLatitude"] ?? "0").Replace(" m", "");
                string newTrackLatitude = ((string)irData.SessionDataDict["WeekendInfo.TrackLatitude"]).Replace(" m", "");
                if (!oldTrackLatitude.Equals(newTrackLatitude))
                {
                    _configs["TrackInfoLatitude"].UpdateSensorState(newTrackLatitude);
                }

                string oldTrackLongitude = ((string)oldIrData?.SessionDataDict["WeekendInfo.TrackLongitude"] ?? "0").Replace(" m", "");
                string newTrackLongitude = ((string)irData.SessionDataDict["WeekendInfo.TrackLongitude"]).Replace(" m", "");
                if (!oldTrackLongitude.Equals(newTrackLongitude))
                {
                    _configs["TrackInfoLongitude"].UpdateSensorState(newTrackLongitude);
                }

                _iRacingNewSession = false;
            }
            else
            {
                bool newDriverIsInCar = irData.Telemetry.DriverMarker;
                bool newDriverIsInReplay = irData.Telemetry.IsReplayPlaying;

                string newSessionType = irData.SessionData.WeekendInfo.EventType ?? "Unknown";
                bool newIsSessionOfficial = ((string)irData.SessionDataDict["WeekendInfo.Official"]).Equals("1");
                int newSessionLeagueId = int.Parse((string)irData.SessionDataDict["WeekendInfo.LeagueID"]);

                string newTrackAltitude = ((string)irData.SessionDataDict["WeekendInfo.TrackAltitude"]).Replace(" m", "");
                string newTrackLatitude = ((string)irData.SessionDataDict["WeekendInfo.TrackLatitude"]).Replace(" m", "");
                string newTrackLongitude = ((string)irData.SessionDataDict["WeekendInfo.TrackLongitude"]).Replace(" m", "");

                _configs["DriverInfoIsInCar"].UpdateSensorState(newDriverIsInCar);
                _configs["DriverInfoIsInReplay"].UpdateSensorState(newDriverIsInReplay);

                _configs["SessionType"].UpdateSensorState(newSessionType);
                _configs["SessionIsOfficial"].UpdateSensorState(newIsSessionOfficial);
                _configs["SessionLeagueId"].UpdateSensorState(newSessionLeagueId);

                _configs["TrackInfoAltitude"].UpdateSensorState(newTrackAltitude);
                _configs["TrackInfoLatitude"].UpdateSensorState(newTrackLatitude);
                _configs["TrackInfoLongitude"].UpdateSensorState(newTrackLongitude);
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

            foreach (KeyValuePair<string, BaseConfig> kvp in _configs)
            {
                if (kvp.Value.ManagedMqttClient.IsStarted && kvp.Value.ManagedMqttClient.IsConnected)
                {
                    kvp.Value.UpdateSensorState(false);
                }
            }

            _mqttClient.StopAsync();
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
        }

        internal void CreateMqttClient()
        {
            _configs = new Dictionary<string, BaseConfig>();
            _mqttFactory = new MqttFactory();
            _mqttClient = _mqttFactory.CreateManagedMqttClient();

            MqttClientOptions mqttClientOptions = new MqttClientOptionsBuilder()
               .WithTcpServer(Settings.Server, Settings.Port)
               .WithCredentials(Settings.Login, Settings.Password)
               .WithClientId($"SimHub-{Environment.MachineName}-{Guid.NewGuid()}")
               .Build();

            ManagedMqttClientOptions managedMqttClientOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(mqttClientOptions)
                .Build();

            _mqttClient.StartAsync(managedMqttClientOptions);

            // create sensor configs - auto publish for home assistant visibility

            BaseConfigDevice driverInfoDevice = new BaseConfigDevice
            {
                Name = $"SimHub - {Environment.MachineName} - Driver Info",
                Identifiers = new[] {
                    $"simhub-driver-info-{UserSettings.UserId}"
                },
                SimHubVersion = (string)PluginManager.GetPropertyValue("DataCorePlugin.SimHubVersion")
            };

            _configs.Add("DriverInfoIsInCar", new BinarySensorConfig(driverInfoDevice, "Is In Car", $"{UserSettings.UserId}-is-in-car", _mqttClient, "mdi:car-select", false));
            _configs.Add("DriverInfoIsInReplay", new BinarySensorConfig(driverInfoDevice, "Is In Replay", $"{UserSettings.UserId}-is-in-car", _mqttClient, "mdi:movie-open-outline", false));

            BaseConfigDevice sessionInfoDevice = new BaseConfigDevice
            {
                Name = $"SimHub - {Environment.MachineName} - Session Info",
                Identifiers = new[] {
                    $"simhub-session-info-{UserSettings.UserId}"
                },
                SimHubVersion = (string) PluginManager.GetPropertyValue("DataCorePlugin.SimHubVersion")
            };

            _configs.Add("SessionInfoType", new SensorConfig(sessionInfoDevice, "Session Type", $"{UserSettings.UserId}-type", _mqttClient, "mdi:form-select"));
            _configs.Add("SessionInfoInSimTime", new SensorConfig(sessionInfoDevice, "In-Sim DateTime", $"{UserSettings.UserId}-in-sim-datetime", _mqttClient, "mdi:clipboard-text-clock-outline"));
            _configs.Add("SessionInfoIsOfficial", new BinarySensorConfig(sessionInfoDevice, "Is Official", $"{UserSettings.UserId}-is-session-official", _mqttClient, "mdi:flag-outline", false, "opening"));
            _configs.Add("SessionInfoLeagueId", new IntegerSensorConfig(sessionInfoDevice, "League Id", $"{UserSettings.UserId}-league-id", _mqttClient, "mdi:identifier", 0));

            BaseConfigDevice trackInfoDevice = new BaseConfigDevice
            {
                Name = $"SimHub - {Environment.MachineName} - Track Info",
                Identifiers = new[] {
                     $"simhub-track-info-{UserSettings.UserId}"
                 },
                SimHubVersion = (string)PluginManager.GetPropertyValue("DataCorePlugin.SimHubVersion")
            };

            _configs.Add("TrackInfoAltitude", new DoubleSensorConfig(trackInfoDevice, "Altitude", $"simhub-track-info-altitude-{UserSettings.UserId}", _mqttClient, "mdi:image-filter-hdr-outline", 0, "m"));
            _configs.Add("TrackInfoLatitude", new DoubleSensorConfig(trackInfoDevice, "Latitude", $"simhub-track-info-latitude-{UserSettings.UserId}", _mqttClient, "mdi:latitude", 0, "\u00b0"));
            _configs.Add("TrackInfoLongitude", new DoubleSensorConfig(trackInfoDevice, "Longitude", $"simhub-track-info-{UserSettings.UserId}-longitude", _mqttClient, "mdi:longitude", 0, "\u00b0"));
        }
    }
}