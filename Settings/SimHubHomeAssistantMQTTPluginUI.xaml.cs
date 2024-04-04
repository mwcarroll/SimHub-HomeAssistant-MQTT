using System.Windows;
using System.Windows.Controls;
using SimHub.HomeAssistant.MQTT.ViewModels;

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
                Login = simHubHomeAssistantMqttPlugin.Settings.Login,
                Password = simHubHomeAssistantMqttPlugin.Settings.Password,
                UserId = simHubHomeAssistantMqttPlugin.UserSettings.UserId,
            };

            DataContext = Model;
        }

        private SimHubHomeAssistantMqttPluginUiModel Model { get; }

        private SimHubHomeAssistantMqttPlugin SimHubHomeAssistantMqttPlugin { get; }

        private void Apply_Settings(object sender, RoutedEventArgs e)
        {
            SimHubHomeAssistantMqttPlugin.Settings.Server = Model.Server;
            SimHubHomeAssistantMqttPlugin.Settings.Login = Model.Login;
            SimHubHomeAssistantMqttPlugin.Settings.Password = Model.Password;

            SimHubHomeAssistantMqttPlugin.CreateMqttClient(false);
        }
    }
}