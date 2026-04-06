using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static DllInjector.Win32Api;
using static DllInjector.Win32Constants;
using System.Diagnostics;
using System.IO;
using DllInjector.Models;

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
                throw new Exception($"Error while creating process: 0x{err:X}");
            }
        }

        public static void InjectOverlayIntoProcess(uint pid, string steamDir, Dictionary<string, string> envs)
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

            var targetAddresses = GetTargetAddresses(isTarget32Bit);

            InjectEnvVariables(isTarget32Bit, hTargetProcess, targetAddresses.SetEnvironmentVariableW, envs);
            
            var unicode = new UnicodeEncoding();
            byte[] dllPathBytes = unicode.GetBytes(dllPath + char.MinValue);

            // write the full path of the DLL into the target process
            IntPtr remotePtrToDllPath = WriteIntoProcess(hTargetProcess, dllPathBytes, (uint)dllPathBytes.Length, PAGE_READWRITE);
            Logger.WriteLine($"[{pid}] Dll path writen to: 0x{remotePtrToDllPath:X}");

            // Inject to the target process
            IntPtr hRemoteLoadLib = CreateRemoteThread(hTargetProcess, IntPtr.Zero, 0, targetAddresses.LoadLibraryW, remotePtrToDllPath, 0, IntPtr.Zero);
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

        public static void InjectEnvVariables(
            bool isTarget32Bit,
            IntPtr hTargetProcess,
            IntPtr setEnvAddress,
            Dictionary<string, string> envs)
        {
            byte[] shellcode = isTarget32Bit
                ? new byte[]
                {
                    // x86 stdcall: arguments are passed via stack
                    0x8B, 0x44, 0x24, 0x04,       // mov eax, [esp+4]      (eax = pointer to ThreadData structure)
                    0xFF, 0x70, 0x08,             // push dword [eax+8]    (push pValue)
                    0xFF, 0x70, 0x04,             // push dword [eax+4]    (push pName)
                    0xFF, 0x10,                   // call dword [eax]      (call pSetEnvVar)
                    0xC2, 0x04, 0x00              // ret 4                 (return from thread)
                }
                : new byte[]
                {
                    // x64 fastcall: arguments are passed in rcx, rdx
                    0x48, 0x83, 0xEC, 0x28,       // sub rsp, 28h          (stack alignment and shadow space)
                    0x49, 0x89, 0xC8,             // mov r8, rcx           (r8 = pointer to ThreadData structure)
                    0x49, 0x8B, 0x48, 0x08,       // mov rcx, [r8+8]       (rcx = pName)
                    0x49, 0x8B, 0x50, 0x10,       // mov rdx, [r8+10h]     (rdx = pValue)
                    0x49, 0x8B, 0x00,             // mov rax, [r8]         (rax = pSetEnvVar)
                    0xFF, 0xD0,                   // call rax              (call function)
                    0x48, 0x83, 0xC4, 0x28,       // add rsp, 28h          (restore stack)
                    0xC3                          // ret                   (return from thread)
                };

            foreach (var env in envs)
            {
                byte[] nameBytes = Encoding.Unicode.GetBytes(env.Key + "\0");
                byte[] valueBytes = Encoding.Unicode.GetBytes(env.Value + "\0");

                // Allocate memory and write strings into the target process
                IntPtr pRemoteName = WriteIntoProcess(hTargetProcess, nameBytes, (uint)nameBytes.Length, PAGE_READWRITE);
                IntPtr pRemoteValue = WriteIntoProcess(hTargetProcess, valueBytes, (uint)valueBytes.Length, PAGE_READWRITE);

                // Form the ThreadData structure: [Function Address] [Name Address] [Value Address]
                int ptrSize = isTarget32Bit ? 4 : 8;
                byte[] dataBytes = new byte[ptrSize * 3];

                if (isTarget32Bit)
                {
                    BitConverter.GetBytes((uint)setEnvAddress).CopyTo(dataBytes, 0);
                    BitConverter.GetBytes((uint)pRemoteName).CopyTo(dataBytes, 4);
                    BitConverter.GetBytes((uint)pRemoteValue).CopyTo(dataBytes, 8);
                }
                else
                {
                    BitConverter.GetBytes((ulong)setEnvAddress).CopyTo(dataBytes, 0);
                    BitConverter.GetBytes((ulong)pRemoteName).CopyTo(dataBytes, 8);
                    BitConverter.GetBytes((ulong)pRemoteValue).CopyTo(dataBytes, 16);
                }

                // Allocate memory and write the structure and shellcode
                IntPtr pRemoteData = WriteIntoProcess(hTargetProcess, dataBytes, (uint)dataBytes.Length, PAGE_READWRITE);
                
                //  Execution rights required for shellcode (PAGE_EXECUTE_READWRITE = 0x40)
                IntPtr pRemoteShellcode = WriteIntoProcess(hTargetProcess, shellcode, (uint)shellcode.Length, 0x40); 

                // Execute the shellcode
                IntPtr hEnvThread = CreateRemoteThread(hTargetProcess, IntPtr.Zero, 0, pRemoteShellcode, pRemoteData, 0, IntPtr.Zero);
                if (hEnvThread == IntPtr.Zero)
                    throw new Exception($"CreateRemoteThread for env vars failed: 0x{Marshal.GetLastWin32Error():X}");

                WaitForSingleObject(hEnvThread, INJ_TIMEOUT);
                CloseHandle(hEnvThread);

                // Clean up allocated memory (0x8000 = MEM_RELEASE)
                VirtualFreeEx(hTargetProcess, pRemoteName, 0, 0x8000);
                VirtualFreeEx(hTargetProcess, pRemoteValue, 0, 0x8000);
                VirtualFreeEx(hTargetProcess, pRemoteData, 0, 0x8000);
                VirtualFreeEx(hTargetProcess, pRemoteShellcode, 0, 0x8000);
            }
            
            // TODO: Write process environment variables to log for debug
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

        public static TargetAddresses GetTargetAddresses(bool is32Bit)
        {
            if (is32Bit)
            {
                string helperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "x32Helper.exe");
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = helperPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();

                    if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                        throw new Exception($"x32Helper failed with exit code {proc.ExitCode}");

                    string[] parts = output.Trim().Split(';');
                    if (parts.Length < 2)
                        throw new Exception("x32Helper returned invalid data format.");

                    return new TargetAddresses
                    {
                        LoadLibraryW = new IntPtr(int.Parse(parts[0])),
                        SetEnvironmentVariableW = new IntPtr(int.Parse(parts[1]))
                    };
                }
            }

            IntPtr hKernel32 = GetModuleHandle("kernel32.dll");
            IntPtr pLoadLib = GetProcAddress(hKernel32, "LoadLibraryW");
            IntPtr pSetEnv = GetProcAddress(hKernel32, "SetEnvironmentVariableW");

            if (pLoadLib == IntPtr.Zero || pSetEnv == IntPtr.Zero)
                throw new Exception("Failed to resolve addresses in 64-bit kernel32.dll");

            return new TargetAddresses
            {
                LoadLibraryW = pLoadLib,
                SetEnvironmentVariableW = pSetEnv
            };
        }

        static IntPtr WriteIntoProcess(IntPtr hProcess, byte[] bytesToWrite, uint bytesSize, uint protect)
        {
            IntPtr remoteAddress = VirtualAllocEx(hProcess, IntPtr.Zero, bytesSize + 1, MEM_COMMIT | MEM_RESERVE, protect);
            if (remoteAddress == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                VirtualFreeEx(hProcess, remoteAddress, bytesSize, MEM_FREE);
                throw new Exception($"VirtualAllocEx error: 0x{err:X}");
            }

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
