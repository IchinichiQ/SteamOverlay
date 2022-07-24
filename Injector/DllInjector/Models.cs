using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DllInjector
{
    internal class Models
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
        }

        [Serializable]
        public class ElevationRequiredException : Exception
        {
            public ElevationRequiredException() { }

            public ElevationRequiredException(string message) : base(message) { }

            public ElevationRequiredException(string message, Exception innerException) : base(message, innerException) { }
        }
    }
}
