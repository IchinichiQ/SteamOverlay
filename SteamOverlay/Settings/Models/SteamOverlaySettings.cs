using System;
using System.Collections.Generic;

namespace SteamOverlay.Settings.Models
{
    public class SteamOverlaySettings : ObservableObject
    {
        private string _steamDir = String.Empty;
        private int _gameId = 0;
        private bool _ENABLE_VK_LAYER_VALVE_steam_overlay_1 = true;
        private bool _isEmptySteamDirMessageViewed = false;

        public string DefaultSteamDir
        {
            get => _steamDir;
            set => SetValue(ref _steamDir, value);
        }

        public int DefaultGameId
        {
            get => _gameId;
            set => SetValue(ref _gameId, value);
        }

        public bool DefaultENABLE_VK_LAYER_VALVE_steam_overlay_1
        {
            get => _ENABLE_VK_LAYER_VALVE_steam_overlay_1;
            set => SetValue(ref _ENABLE_VK_LAYER_VALVE_steam_overlay_1, value);
        }

        public bool IsEmptySteamDirMessageViewed
        {
            get => _isEmptySteamDirMessageViewed;
            set => SetValue(ref _isEmptySteamDirMessageViewed, value);
        }
    }
}