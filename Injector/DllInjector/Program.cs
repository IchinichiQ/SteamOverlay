using System;
using System.Diagnostics;
using System.Collections;
using static DllInjector.Win32Api;
using static DllInjector.Win32Constants;
using static DllInjector.DllInjection;
using Newtonsoft.Json;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using DllInjector.Models;

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
                    Logger.ClearLog();

                // TODO: Add auto launch
                if (Process.GetProcessesByName("steam").Length == 0)
                    throw new Exception("Steam is not running!");

                if (args[0].ToLower() != "-configpath" || args.Length < 2)
                    throw new ArgumentException("Invalid arguments!");

                string configPath = args[1];

                if (!File.Exists(configPath))
                    throw new FileNotFoundException("Config file doesn't exists!");

                string json = File.ReadAllText(configPath);
                GameConfig gameConfig = JsonConvert.DeserializeObject<GameConfig>(json);
                // TODO: Use common config for all games
                InjectorConfig config = gameConfig.injectorConfig;

                if (!File.Exists(config.exePath))
                    throw new FileNotFoundException("Game executable file doesn't exists!");
                if (!Directory.Exists(config.steamDir))
                    throw new DirectoryNotFoundException("Steam directory doesn't exists!");
                if (!File.Exists(Path.Combine(config.steamDir, "GameOverlayRenderer64.dll")))
                    throw new FileNotFoundException("There is no GameOverlayRenderer64.dll file in the steam directory!");
                if (!File.Exists(Path.Combine(config.steamDir, "GameOverlayRenderer.dll")))
                    throw new FileNotFoundException("There is no GameOverlayRenderer.dll file in the steam directory!");
                if (!Directory.Exists(config.workingDir))
                    throw new DirectoryNotFoundException("Working directory doesn't exists!");
                
                // TODO: Log config object instead of parameters
                Logger.WriteLine($"Config path: {configPath}");
                Logger.WriteLine($"Exe: {config.exePath}");
                Logger.WriteLine($"Process name: {config.processName}");
                Logger.WriteLine($"Steam directory: {config.steamDir}");
                Logger.WriteLine($"Working directory: {config.workingDir}");
                Logger.WriteLine($"Arguments: {config.arguments}");
                Logger.WriteLine($"Game id: {config.gameId}");
                Logger.WriteLine($"ENABLE_VK_LAYER_VALVE_steam_overlay_1: {config.ENABLE_VK_LAYER_VALVE_steam_overlay_1}\n");              

                Dictionary<string, string> variables = new Dictionary<string, string>();
                variables.Add("SteamOverlayGameId", config.gameId);
                variables.Add("ENABLE_VK_LAYER_VALVE_steam_overlay_1", config.ENABLE_VK_LAYER_VALVE_steam_overlay_1 ? "1" : "0");

                LaunchAndInject(config, variables);
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("0x5") || ex.ToString().Contains("0x80004005"))
                {
                    Logger.WriteLine("Restarting injector with admin rights...");
                    RunInjectorWithAdminRights($"-configPath {args[1]} -noClearLog");
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

        public static void LaunchAndInject(InjectorConfig config, Dictionary<String, String> environmentVariables)
        {
            // We need debug mode for injection, because process is not our child
            Process.EnterDebugMode();
            
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            
            var currentVariables = Environment.GetEnvironmentVariables();
            string newVariablesString = CreateEnvironmentVariablesString(currentVariables);
            
            CreateNewProcess(config.exePath, config.arguments, config.workingDir, ref pi, CREATE_NEW_CONSOLE | CREATE_UNICODE_ENVIRONMENT, newVariablesString);
            
            for (int injectAttempt = 0; injectAttempt < InjectionAttempts; injectAttempt++)
            {
                try
                {
                    Logger.WriteLine("Waiting for process");

                    var pid = -1;
                    while (pid == -1)
                    {
                        var process = Process.GetProcessesByName(config.processName);
                        if (process.Length > 0)
                        {
                            pid = process.First().Id;
                        }
                        else
                        {
                            Thread.Sleep(10);
                        }
                    }

                    Logger.WriteLine($"Process found, pid = {pid}");
                    
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

        static string CreateEnvironmentVariablesString(IDictionary variables)
        {
            StringBuilder sb = new StringBuilder();
            foreach (DictionaryEntry var in variables)
            {
                sb.Append(var.Key);
                sb.Append('=');
                sb.Append(var.Value);
                sb.Append(Char.MinValue);
            }
            sb.Append(Char.MinValue);

            return sb.ToString();
        }

        static void RunInjectorWithAdminRights(string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName);

            startInfo.UseShellExecute = true;
            startInfo.Arguments = arguments;
            startInfo.Verb = "runas";

            Process.Start(startInfo);
        }
    }
}