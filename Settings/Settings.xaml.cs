using System.Windows;
using System.Windows.Controls;

namespace SimHub.HomeAssistant.MQTT.Settings
{
    public partial class SimHubHomeAssistantMqttPluginUi : UserControl
    {
        public SimHubHomeAssistantMqttPluginUi(SimHubHomeAssistantMqttPlugin simHubHomeAssistantMqttPlugin)
        {
            InitializeComponent();
            SimHubHomeAssistantMqttPlugin = simHubHomeAssistantMqttPlugin;

            Model = new SimHubHomeAssistantMqttPluginUiModel
            {
                Server = simHubHomeAssistantMqttPlugin.Settings.Server,
                Port = simHubHomeAssistantMqttPlugin.Settings.Port,
                Login = simHubHomeAssistantMqttPlugin.Settings.Login,
                Password = simHubHomeAssistantMqttPlugin.Settings.Password,
                UserId = simHubHomeAssistantMqttPlugin.UserSettings.UserId,
                LastError = SimHubHomeAssistantMqttPlugin.Settings.LastError
            };

            DataContext = Model;
        }

        private SimHubHomeAssistantMqttPluginUiModel Model { get; }

        private SimHubHomeAssistantMqttPlugin SimHubHomeAssistantMqttPlugin { get; }

        public void UpdateLastError()
        {
            Model.LastError = SimHubHomeAssistantMqttPlugin.Settings.LastError;
        }

        private void Apply_Settings(object sender, RoutedEventArgs e)
        {
            SimHubHomeAssistantMqttPlugin.Settings.Server = Model.Server;
            SimHubHomeAssistantMqttPlugin.Settings.Port = Model.Port;
            SimHubHomeAssistantMqttPlugin.Settings.Login = Model.Login;
            SimHubHomeAssistantMqttPlugin.Settings.Password = Model.Password;
            SimHubHomeAssistantMqttPlugin.Settings.LastError = Model.LastError;

            SimHubHomeAssistantMqttPlugin.CreateMqttClient();
        }
    }
}