using System;
using System.IO;
using System.Threading;

namespace DllInjector
{
    // TODO: Improve logging
    // - Add timestamps to each log
    // - Break down by logging level
    // - Remove mutexes (why are they here?)
    internal class Logger
    {
        private static string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");

        public static void WriteLine(string message)
        {
            // Process safe
            using (var mutex = new Mutex(false, "SteamOverlayWritingLog"))
            {
                mutex.WaitOne();
                File.AppendAllText(logPath, message + Environment.NewLine);
                mutex.ReleaseMutex();
            }
        }

        public static void ClearLog()
        {
            // Process safe
            using (var mutex = new Mutex(false, "SteamOverlayWritingLog"))
            {
                mutex.WaitOne();
                File.WriteAllText(logPath, "");
                mutex.ReleaseMutex();
            }
        }
    }
}
