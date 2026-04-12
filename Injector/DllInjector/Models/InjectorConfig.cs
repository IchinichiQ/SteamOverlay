namespace DllInjector.Models
{
    // TODO: Use one config class for injector and plugin (move to common solution)
    public class InjectorConfig
    {
        public string SteamDir { get; set; }
        public string ProcessName { get; set; }
        public string WorkingDir { get; set; }
        public ulong GameId { get; set; }
        public bool ENABLE_VK_LAYER_VALVE_steam_overlay_1 { get; set; }
        
        public override string ToString()
        {
            return 
                $"SteamDir: {SteamDir}\n" +
                $"ProcessName: {ProcessName}\n" +
                $"WorkingDir: {WorkingDir}\n" +
                $"GameId: {GameId}\n" +
                $"ENABLE_VK_LAYER_VALVE_steam_overlay_1: {ENABLE_VK_LAYER_VALVE_steam_overlay_1}";
        }
    }
}