using System;
using System.Runtime.InteropServices;

namespace x32Helper
{
    internal class Program
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        static int Main(string[] args)
        {
            IntPtr hModule = GetModuleHandle("kernel32.dll");
            if (hModule == IntPtr.Zero)
            {
                return -1;
            }

            var hLoadLib = GetProcAddress(hModule, "LoadLibraryW");
            if (hLoadLib == IntPtr.Zero)
            {
                return -2;
            }
            
            var hSetEnvVar = GetProcAddress(hModule, "SetEnvironmentVariableW");
            if (hSetEnvVar == IntPtr.Zero)
            {
                return -3;
            }
            
            Console.WriteLine($"{hLoadLib.ToInt32()};{hSetEnvVar.ToInt32()}");

            return 0;
        }
    }
}