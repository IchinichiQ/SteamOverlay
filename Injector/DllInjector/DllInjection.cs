using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using static DllInjector.Win32Api;
using static DllInjector.Win32Constants;
using System.Diagnostics;
using static DllInjector.Models;
using System.IO;

namespace DllInjector
{
    public class DllInjection
    {
        const uint INJ_TIMEOUT = 100000;

        public static IntPtr CreateNewProcess(string exe_path, string cmdArg, string workingDirArg, ref PROCESS_INFORMATION pi, uint flags, string envVars)
        {
            STARTUPINFOW si = new STARTUPINFOW();
            si.cb = (uint)Marshal.SizeOf(si);
            si.dwFlags = STARTF_USESHOWWINDOW;
            si.wShowWindow = SW_SHOW;

            string cmd = null;
            if (!string.IsNullOrEmpty(cmdArg))
                cmd = exe_path + " " + cmdArg;

            string workingDir = workingDirArg == String.Empty ? null : workingDirArg;

            if (CreateProcessW(
                exe_path,
                cmd,
                IntPtr.Zero, //lpProcessAttributes
                IntPtr.Zero, //lpThreadAttributes
                false, //bInheritHandles
                flags, //dwCreationFlags
                envVars, //lpEnvironment 
                workingDir, //lpCurrentDirectory
                ref si, //lpStartupInfo
                out pi //lpProcessInformation
            ))
            {
                Logger.WriteLine($"[{pi.dwProcessId}] Process is successfully created");
                return pi.hProcess;
            }
            else
            {
                int err = Marshal.GetLastWin32Error();

                if (err == ERROR_ELEVATION_REQUIRED)
                    throw new ElevationRequiredException($"Error while creating process: 0x{err:X}");

                throw new Exception($"Error while creating process: 0x{err:X}");
            }
        }

        public static void InjectOverlayIntoProcess(uint pid, string steamDir)
        {
            IntPtr hTargetProcess = OpenProcess(pid);

            bool isTarget32Bit;
            if (!IsWow64Process(hTargetProcess, out isTarget32Bit))
                throw new Exception($"[{pid}] IsWow64Process error: 0x{Marshal.GetLastWin32Error():X}");

            String dllPath;
            if (isTarget32Bit)
                dllPath = Path.Combine(steamDir, "GameOverlayRenderer.dll");
            else
                dllPath = Path.Combine(steamDir, "GameOverlayRenderer64.dll");

            IntPtr loadLibAddr = GetLoadLibWAddress(isTarget32Bit);

            var unicode = new UnicodeEncoding();
            byte[] dllPathBytes = unicode.GetBytes(dllPath + char.MinValue);

            // write the full path of the DLL into the target process
            IntPtr remotePtrToDllPath = WriteIntoProcess(hTargetProcess, dllPathBytes, (uint)dllPathBytes.Length, PAGE_READWRITE);
            Logger.WriteLine($"[{pid}] Dll path writen to: 0x{remotePtrToDllPath:X}");

            // Inject to the target process
            IntPtr hRemoteLoadLib = CreateRemoteThread(hTargetProcess, IntPtr.Zero, 0, loadLibAddr, remotePtrToDllPath, 0, IntPtr.Zero);
            if (hRemoteLoadLib == IntPtr.Zero)
                throw new Exception($"[{pid}] Creating thread failed: 0x{Marshal.GetLastWin32Error():X}");

            int? err = null;
            uint ret = WaitForSingleObject(hRemoteLoadLib, INJ_TIMEOUT);
            if (ret != WAIT_OBJECT_0)
                err = Marshal.GetLastWin32Error();

            CloseHandle(hRemoteLoadLib);
            // cleanup
            VirtualFreeEx(hTargetProcess, remotePtrToDllPath, 0, MEM_FREE);

            if (err != null)
                throw new Exception($"[{pid}] WaitForSingleObject error: 0x{err:X}");

            bool isModuleInProcess = SearchModuleByPath(hTargetProcess, dllPath) != IntPtr.Zero;
            CloseHandle(hTargetProcess);

            if (!isModuleInProcess)
                throw new Exception($"[{pid}] Injection failed! Module is not in the process");

            Logger.WriteLine($"[{pid}] Successfully injected");
        }

        public static IntPtr OpenProcess(uint pid)
        {
            IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

            IntPtr hProcess = Win32Api.OpenProcess(
                PROCESS_CREATE_THREAD | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION,
                false,
                pid
            );

            if (hProcess == IntPtr.Zero || hProcess == INVALID_HANDLE_VALUE)
            {
                int err = Marshal.GetLastWin32Error();
                if (err == ERROR_INVALID_PARAMETER)
                    throw new Exception($"[{pid}] Opening the process failed. Is the process still running?");
                else
                    throw new Exception($"[{pid}] Opening the process failed: 0x{err:X}");
            }

            return hProcess;
        }

        public static IntPtr GetLoadLibWAddress(bool is32Bit)
        {
            IntPtr loadLibAddr;

            if (is32Bit)
            {
                Process x32HelperProc = Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "x32Helper.exe"));
                x32HelperProc.WaitForExit();

                if (x32HelperProc.ExitCode == -1 || x32HelperProc.ExitCode == 0)
                    throw new Exception($"x32HelperProc failed, exit code {x32HelperProc.ExitCode}");

                loadLibAddr = (IntPtr)x32HelperProc.ExitCode;
            }
            else
            {
                IntPtr hModule = GetModuleHandle("kernel32.dll");
                if (hModule == IntPtr.Zero)
                    throw new Exception($"GetModuleHandle error: 0x{Marshal.GetLastWin32Error():X}");

                loadLibAddr = GetProcAddress(hModule, "LoadLibraryW");
                if (loadLibAddr == IntPtr.Zero)
                    throw new Exception($"GetProcAddress error: 0x{Marshal.GetLastWin32Error():X}");
            }

            return loadLibAddr;
        }

        static IntPtr WriteIntoProcess(IntPtr hProcess, byte[] bytesToWrite, uint bytesSize, uint protect)
        {
            IntPtr remoteAddress = VirtualAllocEx(hProcess, IntPtr.Zero, bytesSize, MEM_COMMIT | MEM_RESERVE, protect);
            if (remoteAddress == IntPtr.Zero)
                throw new Exception($"Could not allocate memory in the remote process");

            IntPtr dummy = new IntPtr();
            if (!WriteProcessMemory(hProcess, remoteAddress, bytesToWrite, bytesSize, out dummy))
            {
                int err = Marshal.GetLastWin32Error();
                VirtualFreeEx(hProcess, remoteAddress, bytesSize, MEM_FREE);
                throw new Exception($"WriteProcessMemory error: 0x{err:X}");
            }

            return remoteAddress;
        }

        public static IntPtr SearchModuleByPath(IntPtr hProcess, string searchedName)
        {
            IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
            const uint hModsMax = 0x1000;
            IntPtr[] hMods = new IntPtr[hModsMax];

            uint modules_count = EnumModules(hProcess, hMods, hModsMax, LIST_MODULES_ALL);

            Logger.WriteLine($"[{GetProcessId(hProcess)}] Module list ({modules_count}):");

            IntPtr hNeededModule = IntPtr.Zero;
            for (int i = 0; i < modules_count; i++)
            {
                IntPtr hMod = hMods[i];
                if (hMod == IntPtr.Zero || hMod == INVALID_HANDLE_VALUE) break;

                StringBuilder sb = new StringBuilder((int)MAX_PATH);
                if (GetModuleFileNameExW(hProcess, hMod, sb, MAX_PATH) != 0)
                {
                    string modulePath = sb.ToString();

                    if (modulePath.ToLower() == searchedName.ToLower())
                    {
                        Logger.WriteLine($"{i + 1}. [*] {modulePath}");
                        hNeededModule = hMod;
                    }
                    else
                    {
                        Logger.WriteLine($"{i + 1}. {modulePath}");
                    }
                }
            }

            return hNeededModule;
        }

        static uint EnumModules(IntPtr hProcess, IntPtr[] hMods, uint hModsMax, uint filters)
        {
            uint cbNeeded;
            if (!EnumProcessModulesEx(hProcess, hMods, hModsMax, out cbNeeded, filters))
                throw new Exception($"[Handler {hProcess}] EnumProcessModulesEx failed: 0x{Marshal.GetLastWin32Error():X}");

            uint modules_count = cbNeeded / (uint)Marshal.SizeOf(typeof(IntPtr));

            return modules_count;
        }
    }
}
