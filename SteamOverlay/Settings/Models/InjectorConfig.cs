namespace SteamOverlay.Settings.Models
{
    public class InjectorConfig
    {
        public string SteamDir { get; set; }
        public string ProcessName { get; set; }
        public string WorkingDir { get; set; }
        public ulong GameId { get; set; }
        public bool ENABLE_VK_LAYER_VALVE_steam_overlay_1 { get; set; }
    }
}