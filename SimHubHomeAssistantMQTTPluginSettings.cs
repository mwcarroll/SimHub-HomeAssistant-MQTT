using System;

namespace SimHub.HomeAssistant.MQTT
{
    // ReSharper disable once InconsistentNaming
    public class SimHubHomeAssistantMQTTPluginSettings
    {
        public string Server { get; set; } = "localhost";

        public int Port { get; set; } = 1883;

        public string Login { get; set; } = "admin";

        public string Password { get; set; } = "admin";

        public string LastError { get { return _lastError ?? string.Empty; } set { _lastError = value; } }

        private string _lastError = string.Empty;
    }

    // ReSharper disable once InconsistentNaming
    public class SimHubHomeAssistantMQTTPluginUserSettings
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
    }
}