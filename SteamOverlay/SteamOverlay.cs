using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SteamOverlay.Settings;
using SteamOverlay.Settings.Models;

namespace SteamOverlay
{
    public class SteamOverlay : GenericPlugin
    {
        public override Guid Id { get; } = Guid.Parse("eb3e6a5d-4bc1-4738-a328-cc62959750a1");
        
        private readonly ConfigUtils _configUtils;
        private readonly SteamOverlaySettingsViewModel _settings;
        private readonly string _injectorPath;

        public SteamOverlay(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
            
            var extensionInstallDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _injectorPath = Path.Combine(extensionInstallDir, "DllInjector.exe");
            
            _settings = new SteamOverlaySettingsViewModel(this);
            _configUtils = new ConfigUtils(this);

            InitSettings();
        }

        private void InitSettings()
        {
            if (String.IsNullOrEmpty(_settings.Settings.DefaultSteamDir))
            {
                var steamDir = GetSteamDir();
                if (steamDir == null && !_settings.Settings.IsEmptySteamDirMessageViewed)
                {
                    PlayniteApi.Dialogs.ShowMessage(
                        ResourceProvider.GetString("LOCSteamOverlay_MessageBoxTextEmptySteamDir"),
                        ResourceProvider.GetString("LOCSteamOverlay_MessageBoxTitleEmptySteamDir"),
                        MessageBoxButton.OK);
                    _settings.Settings.IsEmptySteamDirMessageViewed = true;
                    _settings.EndEdit();
                }
                else
                {
                    _settings.Settings.DefaultSteamDir = steamDir;
                }
            }
        }
        
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            // TODO: Add support for selecting multiple games for an operation
            var selectedGame = args.Games.First();
            var menuSection = ResourceProvider.GetString("LOCSteamOverlay_MenuSectionName");
            var isOverlayEnabled = _configUtils.IsGameConfigExists(selectedGame);

            return new List<GameMenuItem>
            {
                new GameMenuItem
                {
                    Description = isOverlayEnabled ? ResourceProvider.GetString("LOCSteamOverlay_MenuItemDisableSteamOverlay") : ResourceProvider.GetString("LOCSteamOverlay_MenuItemEnableSteamOverlay"),
                    MenuSection = menuSection,
                    Action = (a) =>
                    {
                        if (isOverlayEnabled)
                            RemoveOverlayFromGame(selectedGame);
                        else
                            AddOverlayToGame(selectedGame);
                    }
                },
                // TODO: Hide if config not exist for game
                new GameMenuItem
                {
                    Description = ResourceProvider.GetString("LOCSteamOverlay_MenuItemConfigureSteamOverlay"),
                    MenuSection = menuSection,
                    Action = (a) =>
                    {
                        if (!_configUtils.IsGameConfigExists(selectedGame))
                        {
                            PlayniteApi.Dialogs.ShowErrorMessage(ResourceProvider.GetString("LOCSteamOverlay_MessageBoxErrorTextFirstLaunchNeeded"), ResourceProvider.GetString("LOCSteamOverlay_MessageBoxErrorTitle"));
                            return;
                        }

                        var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                        {
                            ShowMaximizeButton = false,
                        });

                        window.Height = 400;
                        window.MinHeight = 400;
                        window.Width = 700;
                        window.MinWidth = 350;
                        window.Title = $"{selectedGame.Name} {ResourceProvider.GetString("LOCSteamOverlay_GameSettingsWindowTitle")}";

                        window.Content = new SteamOverlayGameConfigurationView(this, selectedGame);

                        window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                        window.ShowDialog();
                    }
                }
            };
        }

        private void AddOverlayToGame(Game game)
        {
            InjectorConfig injectorConfig;
            
            if (_configUtils.IsGameConfigExists(game))
            {
                injectorConfig = _configUtils.LoadGameConfig(game).InjectorConfig;
            }
            else
            {
                string processName = null;
                
                var playAction = game.GameActions?.Where(action => action.IsPlayAction).FirstOrDefault();
                if (playAction?.Type == GameActionType.File)
                {
                    processName = Path.GetFileNameWithoutExtension(playAction.Path);
                }
                
                injectorConfig = new InjectorConfig();
                injectorConfig.SteamDir = _settings.Settings.DefaultSteamDir;
                injectorConfig.ProcessName = processName;
                // TODO: Playnite working directory interpritation
                injectorConfig.WorkingDir = game.InstallDirectory;
                injectorConfig.GameId = _settings.Settings.DefaultGameId;
                injectorConfig.ENABLE_VK_LAYER_VALVE_steam_overlay_1 = _settings.Settings.DefaultENABLE_VK_LAYER_VALVE_steam_overlay_1;
            }

            var gameConfig = new GameConfig();
            gameConfig.InjectorConfig = injectorConfig;

            _configUtils.SaveGameConfig(game, gameConfig);
        }

        private void RemoveOverlayFromGame(Game game)
        {
            // TODO: Fix disabling of overlay
            var config = new ConfigUtils(this);
            var gameConfig = config.LoadGameConfig(game);
        }

        private string GetSteamDir()
        {
            var steamDir = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null);
            if (steamDir != null)
            {
                // The path is stored in a registry with "/" instead of "\", so we need to fix it
                return Path.GetFullPath(steamDir);
            }

            var defaultSteamDirs = new[]
            {
                @"C:\Program Files (x86)\Steam",
                @"D:\Program Files (x86)\Steam",
                @"C:\Program Files\Steam",
                @"D:\Program Files\Steam"
            };
            foreach (var defaultSteamDir in defaultSteamDirs)
            {
                if (Directory.Exists(defaultSteamDir))
                {
                    return defaultSteamDir;
                }
            }
            
            return null;
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (!_configUtils.IsGameConfigExists(args.Game))
            {
                return;
            }
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _injectorPath,
                    Arguments = $"-configPath \"{_configUtils.GetGameConfigPath(args.Game)}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return _settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SteamOverlaySettingsView();
        }
    }
}