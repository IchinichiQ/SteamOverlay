using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamOverlay
{
    public class ConfigUtils
    {
        public class InjectorConfig
        {
            public string steamDir { get; set; }
            public string exePath { get; set; }
            public string workingDir { get; set; }
            public string arguments { get; set; }
            public int gameId { get; set; }
            public int resumingDelay { get; set; }
            public bool ENABLE_VK_LAYER_VALVE_steam_overlay_1 { get; set; }
        }

        public class GameConfig
        {
            public InjectorConfig injectorConfig { get; set; }
            public GameAction originalPlayAction { get; set; }
        }

        private string GameConfigsDir;
        private Plugin plugin;

        public ConfigUtils(Plugin plugin)
        {
            this.plugin = plugin;
            GameConfigsDir = Path.Combine(plugin.GetPluginUserDataPath(), "GameConfigs");
        }

        public GameConfig LoadGameConfig(Game game)
        {
            string gameConfigPath = GetGameConfigPath(game);
            string json = File.ReadAllText(gameConfigPath);
            GameConfig config = Serialization.FromJson<GameConfig>(json);

            return config;
        }

        public void SaveGameConfig(Game game, GameConfig config)
        {
            if (!Directory.Exists(GameConfigsDir))
                Directory.CreateDirectory(GameConfigsDir);

            string pathToGameConfig = GetGameConfigPath(game);
            string json = Serialization.ToJson(config, true);
            File.WriteAllText(pathToGameConfig, json);
        }

        public string GetGameConfigPath(Game game)
        {
            return Path.Combine(GameConfigsDir, game.Id.ToString() + ".json");
        }

        public bool IsGameConfigExists(Game game)
        {
            return File.Exists(GetGameConfigPath(game));
        }
    }
}
