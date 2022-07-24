using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamOverlay
{
    public class SteamOverlaySettings : ObservableObject
    {
        private string steamDir = String.Empty;
        private int gameId = 0;
        private int resumingDelay = 0;
        private bool ENABLE_VK_LAYER_VALVE_steam_overlay_1 = true;
        private bool isFirstTimeUse = true;

        public string DefaultSteamDir { get => steamDir; set => SetValue(ref steamDir, value); }
        public int DefaultGameId { get => gameId; set => SetValue(ref gameId, value); }
        public int DefaultResumingDelay { get => resumingDelay; set => SetValue(ref resumingDelay, value); }
        public bool DefaultENABLE_VK_LAYER_VALVE_steam_overlay_1 { get => ENABLE_VK_LAYER_VALVE_steam_overlay_1; set => SetValue(ref ENABLE_VK_LAYER_VALVE_steam_overlay_1, value); }
        public bool IsFirstTimeUse { get => isFirstTimeUse; set => SetValue(ref isFirstTimeUse, value); }
    }

    public class SteamOverlaySettingsViewModel : ObservableObject, ISettings
    {
        private readonly SteamOverlay plugin;
        private SteamOverlaySettings editingClone { get; set; }

        private SteamOverlaySettings settings;
        public SteamOverlaySettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public SteamOverlaySettingsViewModel(SteamOverlay plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<SteamOverlaySettings>();

            // LoadPluginSettings returns null if not saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new SteamOverlaySettings();
            }
        }

        public RelayCommand<object> BrowseSteamDir
        {
            get => new RelayCommand<object>((o) =>
            {
                string filePath = plugin.PlayniteApi.Dialogs.SelectFolder();
                if (!string.IsNullOrEmpty(filePath))
                    Settings.DefaultSteamDir = filePath;
            });
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}