using System;
using System.Runtime.InteropServices;

namespace x32Helper
{
    internal class Program
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(
            IntPtr hModule,
            string procName
        );

        static int Main(string[] args)
        {
            IntPtr hModule = GetModuleHandle("kernel32.dll");
            if (hModule == IntPtr.Zero) return -1;

            IntPtr hLoadLib = GetProcAddress(hModule, "LoadLibraryW");
            if (hLoadLib == IntPtr.Zero) return -1;

            return (int)hLoadLib;
        }
    }
}