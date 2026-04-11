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
using static SteamOverlay.ConfigUtils;

namespace SteamOverlay
{
    public class SteamOverlay : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly ConfigUtils _configUtils;

        private SteamOverlaySettingsViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("eb3e6a5d-4bc1-4738-a328-cc62959750a1");

        private string InjectorPath;
        private string GameConfigsDir;

        public SteamOverlay(IPlayniteAPI api) : base(api)
        {
            string extensionInstallDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            InjectorPath = Path.Combine(extensionInstallDir, "DllInjector.exe");
            GameConfigsDir = Path.Combine(GetPluginUserDataPath(), "GameConfigs");
            settings = new SteamOverlaySettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            if (String.IsNullOrEmpty(settings.Settings.DefaultSteamDir))
            {
                string steamDir = GetSteamDir();
                if (steamDir == null && !settings.Settings.IsEmptySteamDirMessageViewed)
                {
                    PlayniteApi.Dialogs.ShowMessage(ResourceProvider.GetString("LOCSteamOverlay_MessageBoxTextEmptySteamDir"), ResourceProvider.GetString("LOCSteamOverlay_MessageBoxTitleEmptySteamDir"), MessageBoxButton.OK);
                    settings.Settings.IsEmptySteamDirMessageViewed = true;
                    settings.EndEdit();
                }
                else
                {
                    settings.Settings.DefaultSteamDir = steamDir;
                }
            }

            _configUtils = new ConfigUtils(this);
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            // TODO: Add support for selecting multiple games for an operation
            Game selectedGame = args.Games.First();
            var menuSection = ResourceProvider.GetString("LOCSteamOverlay_MenuSectionName");
            bool isOverlayEnabled = _configUtils.IsGameConfigExists(selectedGame);

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
                injectorConfig = _configUtils.LoadGameConfig(game).injectorConfig;
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
                injectorConfig.steamDir = settings.Settings.DefaultSteamDir;
                injectorConfig.processName = processName;
                // TODO: Playnite working directory interpritation
                injectorConfig.workingDir = game.InstallDirectory;
                injectorConfig.gameId = settings.Settings.DefaultGameId;
                injectorConfig.ENABLE_VK_LAYER_VALVE_steam_overlay_1 = settings.Settings.DefaultENABLE_VK_LAYER_VALVE_steam_overlay_1;
            }

            GameConfig gameConfig = new GameConfig();
            gameConfig.injectorConfig = injectorConfig;

            _configUtils.SaveGameConfig(game, gameConfig);
        }

        private void RemoveOverlayFromGame(Game game)
        {
            ConfigUtils config = new ConfigUtils(this);
            GameConfig gameConfig = config.LoadGameConfig(game);
        }

        private string GetSteamDir()
        {
            string steamDir = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null);
            if (steamDir != null)
                // The path is stored in a registry with "/" instead of "\", so we need to fix it
                return Path.GetFullPath(steamDir);

            string defaultSteamDir = @"C:\Program Files (x86)\Steam";
            if (Directory.Exists(defaultSteamDir))
                return defaultSteamDir;

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
                    FileName = InjectorPath,
                    Arguments = $"-configPath \"{_configUtils.GetGameConfigPath(args.Game)}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SteamOverlaySettingsView();
        }
    }
}