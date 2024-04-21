using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SimHub.HomeAssistant.MQTT
{
    public class SimHubHomeAssistantMqttPluginUiModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _server;
        private int _port;
        private string _login;
        private string _password;
        private string _lastError;
        private Guid _userId;

        public string Server
        {
            get => _server;
            set
            {
                _server = value;
                OnPropertyChanged();
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged();
            }
        }

        public string Login
        {
            get => _login;
            set
            {
                _login = value;
                OnPropertyChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
            }
        }

        public string LastError
        {
            get => _lastError;
            set
            {
                _lastError = value;
                OnPropertyChanged();
            }
        }

        public Guid UserId
        {
            get => _userId;
            set
            {
                _userId = value;
                OnPropertyChanged();
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Visibility LastErrorVisible => string.IsNullOrEmpty(_lastError) ? Visibility.Hidden : Visibility.Visible;
    }
}