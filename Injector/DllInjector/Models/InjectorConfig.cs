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
        
        public override string ToString()
        {
            return 
                $"steamDir: {steamDir}\n" +
                $"exePath: {exePath}\n" +
                $"processName: {processName}\n" +
                $"workingDir: {workingDir}\n" +
                $"arguments: {arguments}\n" +
                $"gameId: {gameId}\n" +
                $"ENABLE_VK_LAYER_VALVE_steam_overlay_1: {ENABLE_VK_LAYER_VALVE_steam_overlay_1}";
        }
    }
}