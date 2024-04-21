﻿using System;
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
        private IManagedMqttClient _managedMqttClient;

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

        private int counter = 1;
        private bool iRacingAlreadyInitialized = false;

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
            if (!_managedMqttClient.IsStarted || !_managedMqttClient.IsConnected)
            {
                return;
            }

            if (!data.GameRunning || data.GameName.ToUpper() != "IRACING")
            {
                iRacingAlreadyInitialized = false;

                if (counter == 60)
                {
                    foreach (KeyValuePair<string, BaseConfig> kvp in _configs)
                    {
                        if (kvp.Value.ManagedMqttClient.IsStarted && kvp.Value.ManagedMqttClient.IsConnected)
                        {
                            kvp.Value.UpdateSensorAvailability(false);
                        }
                    }

                    counter = 1;
                }
                else
                {
                    counter++;
                }

                return;
            }

            if (!(data.NewData.GetRawDataObject() is DataSampleEx irData)) return;

            // mark sensors as available
            if (!iRacingAlreadyInitialized)
            {
                foreach (KeyValuePair<string, BaseConfig> kvp in _configs)
                {
                    if (kvp.Value.ManagedMqttClient.IsStarted && kvp.Value.ManagedMqttClient.IsConnected)
                    {
                        kvp.Value.UpdateSensorAvailability(true);
                    }
                }

                iRacingAlreadyInitialized = true;
            }

            #region UpdateEveryTick

            string sessionStartDay = (string)irData.SessionDataDict["WeekendInfo.WeekendOptions.Date"];
            string sessionStartTime = (string)irData.SessionDataDict["WeekendInfo.WeekendOptions.TimeOfDay"];
            int earthRotationSpeedupFactor = int.Parse((string)irData.SessionDataDict["WeekendInfo.WeekendOptions.EarthRotationSpeedupFactor"]);
            double sessionTimePrecise = irData.Telemetry.SessionTime;

            DateTime inSimTime = DateTime.Parse($"{sessionStartDay} {sessionStartTime}").AddSeconds((int)(sessionTimePrecise * earthRotationSpeedupFactor));

            _configs["SessionInfoInSimTime"].UpdateSensorState(inSimTime);

            #endregion

            #region UpdateWhenDifferent
            object oldDataRawObject = data.OldData?.GetRawDataObject();
            if (oldDataRawObject == null || !(oldDataRawObject is DataSampleEx oldIrData))
            {
                _configs["DriverInfoIsOnTrack"].UpdateSensorState(irData.Telemetry.IsOnTrack);
                _configs["DriverInfoIsOnTrackCar"].UpdateSensorState(irData.Telemetry.IsOnTrackCar);
                _configs["DriverInfoIsInReplay"].UpdateSensorState(irData.Telemetry.IsReplayPlaying);

                _configs["SessionInfoType"].UpdateSensorState(irData.SessionData.WeekendInfo.EventType ?? "Unknown");
                _configs["SessionInfoIsOfficial"].UpdateSensorState(((string)irData.SessionDataDict["WeekendInfo.Official"]).Equals("1") ? "Yes" : "No");
                _configs["SessionInfoLeagueId"].UpdateSensorState(int.Parse((string)irData.SessionDataDict["WeekendInfo.LeagueID"]));

                _configs["TrackInfoAltitude"].UpdateSensorState(((string)irData.SessionDataDict["WeekendInfo.TrackAltitude"]).Replace(" m", ""));
                _configs["TrackInfoLatitude"].UpdateSensorState(((string)irData.SessionDataDict["WeekendInfo.TrackLatitude"]).Replace(" m", ""));
                _configs["TrackInfoLongitude"].UpdateSensorState(((string)irData.SessionDataDict["WeekendInfo.TrackLongitude"]).Replace(" m", ""));

                return;
            }

            bool oldDriverInfoIsOnTrack = oldIrData.Telemetry.IsOnTrack;
            bool newDriverInfoIsOnTrack = irData.Telemetry.IsOnTrack;
            if (!oldDriverInfoIsOnTrack.Equals(newDriverInfoIsOnTrack))
            {
                _configs["DriverInfoIsOnTrack"].UpdateSensorState(newDriverInfoIsOnTrack);
            }

            bool oldDriverInfoIsCarOnTrack = oldIrData.Telemetry.IsOnTrackCar;
            bool newDriverInfoIsCarOnTrack = irData.Telemetry.IsOnTrackCar;
            if (!oldDriverInfoIsCarOnTrack.Equals(newDriverInfoIsCarOnTrack))
            {
                _configs["DriverInfoIsOnTrackCar"].UpdateSensorState(newDriverInfoIsCarOnTrack);
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
                _configs["SessionInfoType"].UpdateSensorState(newSessionType);
            }

            string oldIsSessionOfficial = ((string)oldIrData?.SessionDataDict["WeekendInfo.Official"] ?? "2").Equals("1") ? "Yes" : "No";
            string newIsSessionOfficial = ((string)irData.SessionDataDict["WeekendInfo.Official"]).Equals("1") ? "Yes" : "No";
            if (!oldIsSessionOfficial.Equals(newIsSessionOfficial))
            {
                _configs["SessionInfoIsOfficial"].UpdateSensorState(newIsSessionOfficial);
            }

            int oldSessionLeagueId = int.Parse((string)oldIrData?.SessionDataDict["WeekendInfo.LeagueID"] ?? "0");
            int newSessionLeagueId = int.Parse((string)irData.SessionDataDict["WeekendInfo.LeagueID"]);
            if (!oldSessionLeagueId.Equals(newSessionLeagueId))
            {
                _configs["SessionInfoLeagueId"].UpdateSensorState(newSessionLeagueId);
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
                    kvp.Value.UpdateSensorAvailability(false);
                }
            }

            _configs = null;

            if (_managedMqttClient.IsStarted)
            {
                _managedMqttClient.StopAsync();
                _managedMqttClient = null;
            }
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
            Logging.Current.Info("Starting SimHub.HomeAssistant.MQTT");

            // Load settings
            Settings = this.ReadCommonSettings("GeneralSettings", () => new SimHubHomeAssistantMQTTPluginSettings());
            UserSettings = this.ReadCommonSettings("UserSettings", () => new SimHubHomeAssistantMQTTPluginUserSettings());

            CreateMqttClient();
        }

        internal void CreateMqttClient()
        {
            _configs = new Dictionary<string, BaseConfig>();
            _mqttFactory = new MqttFactory();
            _managedMqttClient = _mqttFactory.CreateManagedMqttClient();

            MqttClientOptions mqttClientOptions = new MqttClientOptionsBuilder()
               .WithTcpServer(Settings.Server, Settings.Port)
               .WithCredentials(Settings.Login, Settings.Password)
               .WithClientId($"SimHub-{Environment.MachineName}-{Guid.NewGuid()}")
               .Build();

            ManagedMqttClientOptions managedMqttClientOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(mqttClientOptions)
                .Build();

            _managedMqttClient.StartAsync(managedMqttClientOptions);

            // create sensor configs - auto publish for home assistant visibility
            BaseConfigDevice driverInfoDevice = new BaseConfigDevice
            {
                Name = $"SimHub - {Environment.MachineName} - Driver Info",
                Identifiers = new[] {
                    $"simhub-driver-info-{UserSettings.UserId}"
                },
                SimHubVersion = (string)PluginManager.GetPropertyValue("DataCorePlugin.SimHubVersion")
            };

            _configs.Add("DriverInfoIsOnTrack", new BinarySensorConfig(ref driverInfoDevice, "Is On Track", $"{UserSettings.UserId}-is-on-track", ref _managedMqttClient, "mdi:car-select", false));
            _configs.Add("DriverInfoIsOnTrackCar", new BinarySensorConfig(ref driverInfoDevice, "Is Car On Track", $"{UserSettings.UserId}-is-on-track-car", ref _managedMqttClient, "mdi:car-select", false));
            _configs.Add("DriverInfoIsInReplay", new BinarySensorConfig(ref driverInfoDevice, "Is In Replay", $"{UserSettings.UserId}-is-in-replay", ref _managedMqttClient, "mdi:movie-open-outline", false));

            BaseConfigDevice sessionInfoDevice = new BaseConfigDevice
            {
                Name = $"SimHub - {Environment.MachineName} - Session Info",
                Identifiers = new[] {
                    $"simhub-session-info-{UserSettings.UserId}"
                },
                SimHubVersion = (string) PluginManager.GetPropertyValue("DataCorePlugin.SimHubVersion")
            };

            _configs.Add("SessionInfoType", new SensorConfig(ref sessionInfoDevice, "Session Type", $"{UserSettings.UserId}-type", ref _managedMqttClient, "mdi:form-select"));
            _configs.Add("SessionInfoInSimTime", new SensorConfig(ref sessionInfoDevice, "In-Sim DateTime", $"{UserSettings.UserId}-in-sim-datetime", ref _managedMqttClient, "mdi:clipboard-text-clock-outline"));
            _configs.Add("SessionInfoIsOfficial", new SensorConfig(ref sessionInfoDevice, "Is Official", $"{UserSettings.UserId}-is-session-official", ref _managedMqttClient, "mdi:flag-outline", "No"));
            _configs.Add("SessionInfoLeagueId", new IntegerSensorConfig(ref sessionInfoDevice, "League Id", $"{UserSettings.UserId}-league-id", ref _managedMqttClient, "mdi:identifier", 0));

            BaseConfigDevice trackInfoDevice = new BaseConfigDevice
            {
                Name = $"SimHub - {Environment.MachineName} - Track Info",
                Identifiers = new[] {
                     $"simhub-track-info-{UserSettings.UserId}"
                 },
                SimHubVersion = (string)PluginManager.GetPropertyValue("DataCorePlugin.SimHubVersion")
            };

            _configs.Add("TrackInfoAltitude", new DoubleSensorConfig(ref trackInfoDevice, "Altitude", $"simhub-track-info-altitude-{UserSettings.UserId}", ref _managedMqttClient, "mdi:image-filter-hdr-outline", 0, "m"));
            _configs.Add("TrackInfoLatitude", new DoubleSensorConfig(ref trackInfoDevice, "Latitude", $"simhub-track-info-latitude-{UserSettings.UserId}", ref _managedMqttClient, "mdi:latitude", 0, "\u00b0"));
            _configs.Add("TrackInfoLongitude", new DoubleSensorConfig(ref trackInfoDevice, "Longitude", $"simhub-track-info-{UserSettings.UserId}-longitude", ref _managedMqttClient, "mdi:longitude", 0, "\u00b0"));
        }
    }
}