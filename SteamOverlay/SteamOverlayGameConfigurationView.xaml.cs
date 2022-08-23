using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Controls;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SteamOverlay
{
    public class ConfigData : INotifyPropertyChanged
    {
        private ConfigUtils.GameConfig gameConfig;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ConfigUtils.GameConfig GameConfig
        {
            get { return this.gameConfig; }
            set
            {
                this.gameConfig = value;
                NotifyPropertyChanged();
            }
        }

        public string steamDir
        {
            get { return this.gameConfig.injectorConfig.steamDir; }
            set
            {
                this.gameConfig.injectorConfig.steamDir = value;
                NotifyPropertyChanged();
            }
        }
        public string exePath
        {
            get { return this.gameConfig.injectorConfig.exePath; }
            set
            {
                this.gameConfig.injectorConfig.exePath = value;
                NotifyPropertyChanged();
            }
        }
        public string workingDir
        {
            get { return this.gameConfig.injectorConfig.workingDir; }
            set
            {
                this.gameConfig.injectorConfig.workingDir = value;
                NotifyPropertyChanged();
            }
        }
        public string arguments
        {
            get { return this.gameConfig.injectorConfig.arguments; }
            set
            {
                this.gameConfig.injectorConfig.arguments = value;
                NotifyPropertyChanged();
            }
        }
        public int gameId
        {
            get { return this.gameConfig.injectorConfig.gameId; }
            set
            {
                this.gameConfig.injectorConfig.gameId = value;
                NotifyPropertyChanged();
            }
        }
        public int resumingDelay
        {
            get { return this.gameConfig.injectorConfig.resumingDelay; }
            set
            {
                this.gameConfig.injectorConfig.resumingDelay = value;
                NotifyPropertyChanged();
            }
        }
        public bool ENABLE_VK_LAYER_VALVE_steam_overlay_1
        {
            get { return this.gameConfig.injectorConfig.ENABLE_VK_LAYER_VALVE_steam_overlay_1; }
            set
            {
                this.gameConfig.injectorConfig.ENABLE_VK_LAYER_VALVE_steam_overlay_1 = value;
                NotifyPropertyChanged();
            }
        }
    }

    public partial class SteamOverlayGameConfigurationView : UserControl
    {
        private readonly SteamOverlay plugin;
        private Game game;
        private ConfigUtils configUtils;
        public ConfigData configData { get; set; }

        public SteamOverlayGameConfigurationView(SteamOverlay plugin, Game game)
        {
            this.plugin = plugin;
            this.game = game;

            configUtils = new ConfigUtils(plugin);
            configData = new ConfigData();
            configData.GameConfig = configUtils.LoadGameConfig(game);

            InitializeComponent();
        }

        private void ButtonBrowseSteamDir_Click(object sender, RoutedEventArgs e)
        {
            string steamDir = plugin.PlayniteApi.Dialogs.SelectFolder();
            if (steamDir != String.Empty)
                configData.steamDir = steamDir;
        }

        private void ButtonBrowseExeFile_Click(object sender, RoutedEventArgs e)
        {
            string exePath = plugin.PlayniteApi.Dialogs.SelectFile(ResourceProvider.GetString("LOCSteamOverlay_DialogSelectFileGameExe") + "|*.*");
            if (exePath != String.Empty)
                configData.exePath = exePath;
        }

        private void ButtonBrowseWorkingDir_Click(object sender, RoutedEventArgs e)
        {
            string workDir = plugin.PlayniteApi.Dialogs.SelectFolder();
            if (workDir != String.Empty)
                configData.workingDir = workDir;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            configUtils = new ConfigUtils(plugin);
            configUtils.SaveGameConfig(game, configData.GameConfig);

            Window.GetWindow(this).Close();
        }

        private void ButtonSearchGame_Click(object sender, RoutedEventArgs e)
        {
            var window = plugin.PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions());

            window.Height = 600;
            window.MinHeight = 300;
            window.Width = 600;
            window.MinWidth = 300;
            window.Title = ResourceProvider.GetString("LOCSteamOverlay_SteamSearchWindowTitle");

            var gameSearchView = new SteamGameSearch.SteamGameSearchView(plugin, game.Name);
            window.Content = gameSearchView;

            window.Owner = plugin.PlayniteApi.Dialogs.GetCurrentAppWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            window.ShowDialog();

            if (gameSearchView.selectedGame != null)
                TextBoxGameId.Text = gameSearchView.selectedGame.Id.ToString();
        }
    }
}
