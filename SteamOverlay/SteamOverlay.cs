using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static SteamOverlay.ConfigUtils;

namespace SteamOverlay
{
    public class SteamOverlay : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

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
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            // TODO: Add support for selecting multiple games for an operation
            Game selectedGame = args.Games.First();
            var menuSection = ResourceProvider.GetString("LOCSteamOverlay_MenuSectionName");
            bool hasOverlayAction = GameHasOverlayAction(selectedGame);

            return new List<GameMenuItem>
            {
                new GameMenuItem
                {
                    Description = hasOverlayAction ? ResourceProvider.GetString("LOCSteamOverlay_MenuItemDisableSteamOverlay") : ResourceProvider.GetString("LOCSteamOverlay_MenuItemEnableSteamOverlay"),
                    MenuSection = menuSection,
                    Action = (a) =>
                    {

                        if (settings.Settings.IsFirstTimeEnablingOverlay)
                        {
                            MessageBoxResult res = PlayniteApi.Dialogs.ShowMessage(ResourceProvider.GetString("LOCSteamOverlay_MessageBoxTextFirstTimeUse"), ResourceProvider.GetString("LOCSteamOverlay_MessageBoxTitleFirstTimeUse"), MessageBoxButton.YesNo);
                            if (res == MessageBoxResult.No)
                                return;

                            settings.Settings.IsFirstTimeEnablingOverlay = false;
                            settings.EndEdit();
                        }

                        if (hasOverlayAction)
                            RemoveOverlayFromGame(selectedGame);
                        else
                            AddOverlayToGame(selectedGame);
                    }
                },
                new GameMenuItem
                {
                    Description = ResourceProvider.GetString("LOCSteamOverlay_MenuItemConfigureSteamOverlay"),
                    MenuSection = menuSection,
                    Action = (a) =>
                    {
                        ConfigUtils configUtils = new ConfigUtils(this);
                        if (!configUtils.IsGameConfigExists(selectedGame))
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
            GameAction[] playActions = game.GameActions.Where(action => action.IsPlayAction).ToArray();
            if (playActions.Length == 0)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(ResourceProvider.GetString("LOCSteamOverlay_MessageBoxErrorTextNoPlayAction"), ResourceProvider.GetString("LOCSteamOverlay_MessageBoxErrorTitle"));
                return;
            }
            if (playActions.Length > 1)
            {
                // TODO: Add support for several play actions
                PlayniteApi.Dialogs.ShowErrorMessage(ResourceProvider.GetString("LOCSteamOverlay_MessageBoxErrorTextSeveralPlayActions"), ResourceProvider.GetString("LOCSteamOverlay_MessageBoxErrorTitle"));
                return;
            }
            if (playActions[0].Type != GameActionType.File)
            {
                // TODO: Add support for other action types
                PlayniteApi.Dialogs.ShowErrorMessage(ResourceProvider.GetString("LOCSteamOverlay_MessageBoxErrorTextNotFileType"), ResourceProvider.GetString("LOCSteamOverlay_MessageBoxErrorTitle"));
                return;
            }
            GameAction playAction = playActions[0];

            string pathToGameExecutable = GetActionFileAbsolutePath(game, playAction);

            InjectorConfig injectorConfig;

            ConfigUtils configUtils = new ConfigUtils(this);
            if (configUtils.IsGameConfigExists(game))
            {
                injectorConfig = configUtils.LoadGameConfig(game).injectorConfig;
            }
            else
            {
                injectorConfig = new InjectorConfig();
                injectorConfig.steamDir = settings.Settings.DefaultSteamDir;
                injectorConfig.exePath = pathToGameExecutable;
                // TODO: Playnite working directory interpritation
                injectorConfig.workingDir = Path.GetDirectoryName(pathToGameExecutable);
                injectorConfig.arguments = playAction.Arguments;
                injectorConfig.gameId = settings.Settings.DefaultGameId;
                injectorConfig.resumingDelay = settings.Settings.DefaultResumingDelay;
                injectorConfig.ENABLE_VK_LAYER_VALVE_steam_overlay_1 = settings.Settings.DefaultENABLE_VK_LAYER_VALVE_steam_overlay_1;
            }

            GameConfig gameConfig = new GameConfig();
            gameConfig.originalPlayAction = playAction;
            gameConfig.injectorConfig = injectorConfig;

            configUtils.SaveGameConfig(game, gameConfig);

            GameAction actionOverlay = new GameAction();
            actionOverlay.IsPlayAction = true;
            actionOverlay.Name = "SteamOverlay";
            actionOverlay.Type = GameActionType.File;
            actionOverlay.Path = InjectorPath;
            actionOverlay.Arguments = $"-configPath \"{configUtils.GetGameConfigPath(game)}\"";
            // Working dir is not needed because injector will use working dir from config
            //actionOverlay.WorkingDir = playAction.WorkingDir;
            actionOverlay.TrackingPath = playAction.TrackingPath;
            actionOverlay.TrackingMode = playAction.TrackingMode;

            game.GameActions.Remove(playAction);
            game.GameActions.Add(actionOverlay);
        }

        private void RemoveOverlayFromGame(Game game)
        {
            // TODO: Handle situation when game config file is not exist
            ConfigUtils config = new ConfigUtils(this);
            GameConfig gameConfig = config.LoadGameConfig(game);

            GameAction overlayAction = game.GameActions.First(action => action.Name == "SteamOverlay");

            game.GameActions.Remove(overlayAction);
            game.GameActions.Add(gameConfig.originalPlayAction);
        }

        private bool GameHasOverlayAction(Game game)
        {
            return game.GameActions.Any(action => action.Name == "SteamOverlay");
        }

        private string GetActionFileAbsolutePath(Game game, GameAction action)
        {
            // TODO: Playnite variables interpritation
            if (File.Exists(action.Path))
            {
                return action.Path;
            }
            else
            {
                string absolutePath = Path.Combine(game.InstallDirectory, action.Path);
                if (File.Exists(absolutePath))
                {
                    return absolutePath;
                }
                else
                {
                    throw new FileNotFoundException(ResourceProvider.GetString("LOCSteamOverlay_ErrorPlayActionFileNotFound"));
                }
            }
        }

        private string GetSteamDir()
        {
            string steamDir = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null);
            if (steamDir != null)
                return steamDir;

            string defaultSteamDir = @"C:\Program Files (x86)\Steam";
            if (Directory.Exists(defaultSteamDir))
                return defaultSteamDir;

            return null;
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            // Add code to be executed when game is finished installing.
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            // Add code to be executed when game is started running.
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Add code to be executed when Playnite is initialized.
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            // Add code to be executed when library is updated.
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