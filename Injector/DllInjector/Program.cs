using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections;
using static DllInjector.Win32Api;
using static DllInjector.Win32Constants;
using static DllInjector.DllInjection;
using static DllInjector.Models;
using Newtonsoft.Json;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace DllInjector
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (!args.Contains("-noClearLog"))
                    Logger.ClearLog();

                if (Process.GetProcessesByName("steam").Length == 0)
                    throw new Exception("Steam is not running!");

                if (args[0].ToLower() != "-configpath" || args.Length < 2)
                    throw new ArgumentException("Invalid arguments!");

                string configPath = args[1];

                if (!File.Exists(configPath))
                    throw new FileNotFoundException("Config file doesn't exists!");

                string json = File.ReadAllText(configPath);
                GameConfig gameConfig = JsonConvert.DeserializeObject<GameConfig>(json);
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

                Logger.WriteLine($"Config path: {configPath}");
                Logger.WriteLine($"Exe: {config.exePath}");
                Logger.WriteLine($"Steam directory: {config.steamDir}");
                Logger.WriteLine($"Working directory: {config.workingDir}");
                Logger.WriteLine($"Arguments: {config.arguments}");
                Logger.WriteLine($"Game id: {config.gameId}");
                Logger.WriteLine($"Resuming delay: {config.resumingDelay}");
                Logger.WriteLine($"ENABLE_VK_LAYER_VALVE_steam_overlay_1: {config.ENABLE_VK_LAYER_VALVE_steam_overlay_1}\n");              

                Dictionary<string, string> variables = new Dictionary<string, string>();
                variables.Add("SteamOverlayGameId", config.gameId.ToString());
                variables.Add("ENABLE_VK_LAYER_VALVE_steam_overlay_1", config.ENABLE_VK_LAYER_VALVE_steam_overlay_1 ? "1" : "0");
                //variables.Add("SteamGameId", gameId);

                LaunchAndInject(config, variables);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(ex.ToString());

                if (ex is ElevationRequiredException)
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
                    Console.ReadKey();
                }
            }

            Logger.WriteLine("Closing...");
        }

        public static uint LaunchAndInject(InjectorConfig config, Dictionary<String, String> environmentVariables)
        {
            // We don't need debug mode for injection, because process will be our child
            //Process.EnterDebugMode();

            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

            var currentVariables = Environment.GetEnvironmentVariables();
            foreach (KeyValuePair<String, String> var in environmentVariables)
                currentVariables[var.Key] = var.Value;

            string newVariablesString = CreateEnvironmentVariablesString(currentVariables);

            CreateNewProcess(config.exePath, config.arguments, config.workingDir, ref pi, CREATE_SUSPENDED | CREATE_NEW_CONSOLE | CREATE_UNICODE_ENVIRONMENT, newVariablesString);

            InjectOverlayIntoProcess(pi.dwProcessId, config.steamDir);

            // Without waiting, playnite might skip a child process (it checks them every 500ms), causing playtime to be counted incorrectly
            Thread.Sleep(config.resumingDelay);
            if (ResumeThread(pi.hThread) == 4294967295)
                throw new Exception($"[{pi.dwProcessId}] Thread resume failed: 0x{Marshal.GetLastWin32Error():X}");

            Logger.WriteLine($"[{pi.dwProcessId}] Thread resumed");

            return pi.dwProcessId;
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