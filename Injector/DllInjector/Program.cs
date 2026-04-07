using System;
using System.Diagnostics;
using static DllInjector.WinApi.Win32Api;
using static DllInjector.WinApi.Win32Constants;
using static DllInjector.DllInjection;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using DllInjector.Models;
using DllInjector.WinApi.Models;

namespace DllInjector
{
    internal class Program
    {
        private const int InjectionAttempts = 3;
        
        static void Main(string[] args)
        {
            try
            {
                // TODO: Make logs auto clear by size limit
                if (!args.Contains("-noClearLog"))
                {
                    Logger.ClearLog();
                }

                // TODO: Auto launch
                if (Process.GetProcessesByName("steam").Length == 0)
                {
                    throw new Exception("Steam is not running!");
                }

                if (args[0].ToLower() != "-configpath" || args.Length < 2)
                {
                    throw new ArgumentException("Invalid arguments!");
                }

                var configPath = args[1];
                var config = ReadConfig(configPath);
                
                ValidateConfig(config);
                
                Logger.WriteLine($"Config path: {configPath}");
                Logger.WriteLine($"Config:\n{config}");

                var variables = new Dictionary<string, string>
                {
                    { "SteamOverlayGameId", config.gameId },
                    { "ENABLE_VK_LAYER_VALVE_steam_overlay_1", config.ENABLE_VK_LAYER_VALVE_steam_overlay_1 ? "1" : "0" }
                };

                LaunchAndInject(config, variables);
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("0x5") || ex.ToString().Contains("0x80004005"))
                {
                    Logger.WriteLine("Restarting injector with admin rights...");
                    RestartWithAdminRights($"-configPath {args[1]} -noClearLog");
                }
                else
                {
                    AllocConsole();
                    Console.Title = "SteamOverlay plugin";

                    Console.WriteLine(ex.Message + "\n");
                    Console.WriteLine(ex.ToString());

                    Console.WriteLine("\n" + "Press any key to exit...");
                    Console.Read();
                }
            }

            Logger.WriteLine("Closing...");
        }

        private static InjectorConfig ReadConfig(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Config file doesn't exists!");
            }
            
            var json = File.ReadAllText(path);
            var gameConfig = JsonConvert.DeserializeObject<GameConfig>(json);
            
            // TODO: Use common config fields for all games
            return gameConfig.injectorConfig;
        }
        
        private static void ValidateConfig(InjectorConfig config)
        {
            if (!File.Exists(config.exePath))
            {
                throw new FileNotFoundException("Game executable file doesn't exists!");
            }

            if (!Directory.Exists(config.steamDir))
            {
                throw new DirectoryNotFoundException("Steam directory doesn't exists!");
            }

            if (!File.Exists(Path.Combine(config.steamDir, "GameOverlayRenderer64.dll")))
            {
                throw new FileNotFoundException("There is no GameOverlayRenderer64.dll file in the steam directory!");
            }

            if (!File.Exists(Path.Combine(config.steamDir, "GameOverlayRenderer.dll")))
            {
                throw new FileNotFoundException("There is no GameOverlayRenderer.dll file in the steam directory!");
            }

            if (!Directory.Exists(config.workingDir))
            {
                throw new DirectoryNotFoundException("Working directory doesn't exists!");
            }
        }
        
        private static void LaunchAndInject(InjectorConfig config, Dictionary<String, String> environmentVariables)
        {
            // We need debug mode for injection, because process is not our child
            Process.EnterDebugMode();
            
            var pi = new PROCESS_INFORMATION();
            CreateNewProcess(config.exePath, config.arguments, config.workingDir, ref pi, CREATE_NEW_CONSOLE | CREATE_UNICODE_ENVIRONMENT);
            
            // We need retries because some games quickly respawn the process,
            // so the first one we catch might not be the right one
            for (var injectAttempt = 0; injectAttempt < InjectionAttempts; injectAttempt++)
            {
                try
                {
                    var pid = WaitForProcess(config.processName);
                    InjectOverlayIntoProcess((uint)pid, config.steamDir, environmentVariables);

                    Logger.WriteLine("Overlay injected");

                    break;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Exception while injecting: {ex}");
                }
            }
        }

        // TODO: Maybe wait for any process in folder? To support launcher based games where we know only install dir
        private static int WaitForProcess(string processName)
        {
            Logger.WriteLine("Waiting for process");

            var pid = -1;
            while (pid == -1)
            {
                var process = Process.GetProcessesByName(processName);
                if (process.Length > 0)
                {
                    Logger.WriteLine($"Process found, pid = {pid}");
                    
                    pid = process.First().Id;
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
            
            return pid;
        }
        
        static void RestartWithAdminRights(string arguments)
        {
            var startInfo = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName);

            startInfo.UseShellExecute = true;
            startInfo.Arguments = arguments;
            startInfo.Verb = "runas";

            Process.Start(startInfo);
        }
    }
}