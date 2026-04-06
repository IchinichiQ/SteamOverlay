namespace DllInjector.Models
{
    public class InjectorConfig
    {
        public string steamDir { get; set; }
        public string exePath { get; set; }
        public string processName { get; set; }
        public string workingDir { get; set; }
        public string arguments { get; set; }
        public string gameId { get; set; }
        public bool ENABLE_VK_LAYER_VALVE_steam_overlay_1 { get; set; }
    }
}