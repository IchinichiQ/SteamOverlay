using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Models;
using SteamOverlay.Settings.Models;

namespace SteamOverlay.Settings
{
    public partial class SteamOverlayGameConfigurationView : UserControl
    {
        public ConfigData ConfigData { get; }
        
        private readonly SteamOverlay _plugin;
        private readonly Game _game;
        private readonly ConfigUtils _configUtils;

        public SteamOverlayGameConfigurationView(SteamOverlay plugin, Game game)
        {
            _plugin = plugin;
            _game = game;

            _configUtils = new ConfigUtils(plugin);
            ConfigData = new ConfigData
            {
                GameConfig = _configUtils.LoadGameConfig(game)
            };

            InitializeComponent();
        }

        private void ButtonBrowseSteamDir_Click(object sender, RoutedEventArgs e)
        {
            var steamDir = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (string.IsNullOrEmpty(steamDir))
            {
                return;
            }
            
            ConfigData.SteamDir = steamDir;
        }

        private void ButtonBrowseExeFile_Click(object sender, RoutedEventArgs e)
        {
            var path = _plugin.PlayniteApi.Dialogs.SelectFile(ResourceProvider.GetString("LOCSteamOverlay_DialogSelectFileGameExe") + "|*.*");
            var filename = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(filename))
            {
                return;
            }
            
            ConfigData.ProcessName = filename;
        }

        private void ButtonBrowseWorkingDir_Click(object sender, RoutedEventArgs e)
        {
            var workingDir = _plugin.PlayniteApi.Dialogs.SelectFolder();
            if (string.IsNullOrEmpty(workingDir))
            {
                return;
            }
            
            ConfigData.WorkingDir = workingDir;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            _configUtils.SaveGameConfig(_game, ConfigData.GameConfig);

            Window.GetWindow(this).Close();
        }

        private void ButtonSearchGame_Click(object sender, RoutedEventArgs e)
        {
            var window = _plugin.PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions());

            window.Height = 600;
            window.MinHeight = 300;
            window.Width = 600;
            window.MinWidth = 300;
            window.Title = ResourceProvider.GetString("LOCSteamOverlay_SteamSearchWindowTitle");

            var gameSearchView = new SteamGameSearch.SteamGameSearchView(_plugin, _game.Name);
            window.Content = gameSearchView;

            window.Owner = _plugin.PlayniteApi.Dialogs.GetCurrentAppWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            window.ShowDialog();

            if (gameSearchView.selectedGame != null)
            {
                TextBoxGameId.Text = gameSearchView.selectedGame.Id.ToString();
            }
        }
    }
}
