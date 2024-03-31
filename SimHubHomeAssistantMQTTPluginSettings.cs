﻿using System;

namespace SimHub.HomeAssistant.MQTT
{
    /// <summary>
    /// Settings class, make sure it can be correctly serialized using JSON.net
    /// </summary>
    public class SimHubHomeAssistantMQTTPluginSettings
    {
        public string Server { get; set; } = "localhost";

        public string Login { get; set; } = "admin";

        public string Password { get; set; } = "admin";
    }

    public class SimHubHomeAssistantMQTTPluginUserSettings
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
    }
}