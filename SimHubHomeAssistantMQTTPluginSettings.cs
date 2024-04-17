using System;

namespace SimHub.HomeAssistant.MQTT
{
    public class SimHubHomeAssistantMQTTPluginSettings
    {
        public string Server { get; set; } = "localhost";

        public int Port { get; set; } = 1883;

        public string Login { get; set; } = "admin";

        public string Password { get; set; } = "admin";
    }

    public class SimHubHomeAssistantMQTTPluginUserSettings
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
    }
}