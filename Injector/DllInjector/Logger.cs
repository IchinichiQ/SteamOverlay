using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DllInjector
{
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
