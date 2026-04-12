using System.IO;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using SteamOverlay.Settings.Models;

namespace SteamOverlay.Settings
{
    public class ConfigUtils
    {
        private readonly string _gameConfigsDir;

        public ConfigUtils(Plugin plugin)
        {
            _gameConfigsDir = Path.Combine(plugin.GetPluginUserDataPath(), "GameConfigs");
        }

        public GameConfig LoadGameConfig(Game game)
        {
            var gameConfigPath = GetGameConfigPath(game);
            var json = File.ReadAllText(gameConfigPath);
            var config = Serialization.FromJson<GameConfig>(json);

            return config;
        }

        public void SaveGameConfig(Game game, GameConfig config)
        {
            if (!Directory.Exists(_gameConfigsDir))
            {
                Directory.CreateDirectory(_gameConfigsDir);
            }

            var pathToGameConfig = GetGameConfigPath(game);
            var json = Serialization.ToJson(config, true);
            File.WriteAllText(pathToGameConfig, json);
        }

        public string GetGameConfigPath(Game game)
        {
            return Path.Combine(_gameConfigsDir, game.Id + ".json");
        }

        public bool IsGameConfigExists(Game game)
        {
            return File.Exists(GetGameConfigPath(game));
        }
    }
}
