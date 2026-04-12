using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SteamOverlay.Settings.Models
{
    public class ConfigData : INotifyPropertyChanged
    {
        private GameConfig _gameConfig;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public GameConfig GameConfig
        {
            get => _gameConfig;
            set
            {
                _gameConfig = value;
                NotifyPropertyChanged();
            }
        }

        public string steamDir
        {
            get => _gameConfig.InjectorConfig.SteamDir;
            set
            {
                _gameConfig.InjectorConfig.SteamDir = value;
                NotifyPropertyChanged();
            }
        }
        public string processName
        {
            get => _gameConfig.InjectorConfig.ProcessName;
            set
            {
                _gameConfig.InjectorConfig.ProcessName = value;
                NotifyPropertyChanged();
            }
        }
        public string workingDir
        {
            get => _gameConfig.InjectorConfig.WorkingDir;
            set
            {
                _gameConfig.InjectorConfig.WorkingDir = value;
                NotifyPropertyChanged();
            }
        }
        public int gameId
        {
            get => _gameConfig.InjectorConfig.GameId;
            set
            {
                _gameConfig.InjectorConfig.GameId = value;
                NotifyPropertyChanged();
            }
        }
        public bool ENABLE_VK_LAYER_VALVE_steam_overlay_1
        {
            get => _gameConfig.InjectorConfig.ENABLE_VK_LAYER_VALVE_steam_overlay_1;
            set
            {
                _gameConfig.InjectorConfig.ENABLE_VK_LAYER_VALVE_steam_overlay_1 = value;
                NotifyPropertyChanged();
            }
        }
    }
}