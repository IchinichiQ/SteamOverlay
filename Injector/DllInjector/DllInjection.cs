using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.IO;
using DllInjector.Models;
using DllInjector.WinApi;
using DllInjector.WinApi.Models;

namespace DllInjector
{
    public class DllInjection
    {
        const uint INJ_TIMEOUT = 100000;

        // Will be soon deleted as injector now works on the fly
        public static void CreateNewProcess(string exePath, string cmdArg, string workingDirArg, ref PROCESS_INFORMATION pi, uint flags)
        {
            var si = new STARTUPINFOW();
            si.cb = (uint)Marshal.SizeOf(si);
            si.dwFlags = Win32Constants.STARTF_USESHOWWINDOW;
            si.wShowWindow = Win32Constants.SW_SHOW;

            string cmd = null;
            if (!string.IsNullOrEmpty(cmdArg))
            {
                cmd = exePath + " " + cmdArg;
            }

            var workingDir = workingDirArg == String.Empty ? null : workingDirArg;
            if (!Win32Api.CreateProcessW(
                    exePath,
                    cmd,
                    lpProcessAttributes: IntPtr.Zero,
                    lpThreadAttributes: IntPtr.Zero,
                    bInheritHandles: false,
                    dwCreationFlags: flags,
                    lpEnvironment: null,
                    lpCurrentDirectory: workingDir,
                    lpStartupInfo: ref si,
                    lpProcessInformation: out pi
                ))
            {
                var err = Marshal.GetLastWin32Error();
                throw new Exception($"Error while creating process: 0x{err:X}");
            }
            
            Logger.WriteLine($"[{pi.dwProcessId}] Process is successfully created");
        }

        public static void InjectOverlayIntoProcess(uint pid, string steamDir, Dictionary<string, string> envs)
        {
            var hTargetProcess = OpenProcess(pid);

            if (!Win32Api.IsWow64Process(hTargetProcess, out var isTarget32Bit))
            {
                throw new Exception($"[{pid}] IsWow64Process error: 0x{Marshal.GetLastWin32Error():X}");
            }

            var dllPath = isTarget32Bit
                ? Path.Combine(steamDir, "GameOverlayRenderer.dll")
                : Path.Combine(steamDir, "GameOverlayRenderer64.dll");

            var targetAddresses = GetTargetAddresses(isTarget32Bit);

            InjectEnvVariables(isTarget32Bit, hTargetProcess, targetAddresses.SetEnvironmentVariableW, envs);
            
            var unicode = new UnicodeEncoding();
            var dllPathBytes = unicode.GetBytes(dllPath + char.MinValue);

            // Write the full path of the DLL into the target process
            var remotePtrToDllPath = WriteIntoProcess(hTargetProcess, dllPathBytes, (uint)dllPathBytes.Length, Win32Constants.PAGE_READWRITE);
            Logger.WriteLine($"[{pid}] Dll path writen to: 0x{remotePtrToDllPath:X}");

            // Inject to the target process
            var hRemoteLoadLib = Win32Api.CreateRemoteThread(hTargetProcess, IntPtr.Zero, 0, targetAddresses.LoadLibraryW, remotePtrToDllPath, 0, IntPtr.Zero);
            if (hRemoteLoadLib == IntPtr.Zero)
            {
                throw new Exception($"[{pid}] Creating thread failed: 0x{Marshal.GetLastWin32Error():X}");
            }

            int? err = null;
            var waitResult = Win32Api.WaitForSingleObject(hRemoteLoadLib, INJ_TIMEOUT);
            if (waitResult != Win32Constants.WAIT_OBJECT_0)
            {
                err = Marshal.GetLastWin32Error();
            }

            // cleanup
            Win32Api.CloseHandle(hRemoteLoadLib);
            Win32Api.VirtualFreeEx(hTargetProcess, remotePtrToDllPath, 0, Win32Constants.MEM_RELEASE);

            if (err != null)
            {
                throw new Exception($"[{pid}] WaitForSingleObject error: 0x{err:X}");
            }

            var isModuleInProcess = SearchModuleByPath(hTargetProcess, dllPath) != IntPtr.Zero;
            Win32Api.CloseHandle(hTargetProcess);

            if (!isModuleInProcess)
            {
                throw new Exception($"[{pid}] Injection failed! Module is not in the process");
            }

            Logger.WriteLine($"[{pid}] Successfully injected");
        }

        private static void InjectEnvVariables(
            bool isTarget32Bit,
            IntPtr hTargetProcess,
            IntPtr setEnvAddress,
            Dictionary<string, string> envs)
        {
            var shellcode = isTarget32Bit
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
                var nameBytes = Encoding.Unicode.GetBytes(env.Key + "\0");
                var valueBytes = Encoding.Unicode.GetBytes(env.Value + "\0");

                // Allocate memory and write strings into the target process
                var pRemoteName = WriteIntoProcess(hTargetProcess, nameBytes, (uint)nameBytes.Length, Win32Constants.PAGE_READWRITE);
                var pRemoteValue = WriteIntoProcess(hTargetProcess, valueBytes, (uint)valueBytes.Length, Win32Constants.PAGE_READWRITE);

                // Form the ThreadData structure: [Function Address] [Name Address] [Value Address]
                var ptrSize = isTarget32Bit ? 4 : 8;
                var dataBytes = new byte[ptrSize * 3];

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
                var pRemoteData = WriteIntoProcess(hTargetProcess, dataBytes, (uint)dataBytes.Length, Win32Constants.PAGE_READWRITE);
                
                //  Execution rights required for shellcode (PAGE_EXECUTE_READWRITE = 0x40)
                var pRemoteShellcode = WriteIntoProcess(hTargetProcess, shellcode, (uint)shellcode.Length, 0x40); 

                // Execute the shellcode
                var hEnvThread = Win32Api.CreateRemoteThread(hTargetProcess, IntPtr.Zero, 0, pRemoteShellcode, pRemoteData, 0, IntPtr.Zero);
                if (hEnvThread == IntPtr.Zero)
                    throw new Exception($"CreateRemoteThread for env vars failed: 0x{Marshal.GetLastWin32Error():X}");

                Win32Api.WaitForSingleObject(hEnvThread, INJ_TIMEOUT);
                Win32Api.CloseHandle(hEnvThread);

                // Clean up allocated memory
                Win32Api.VirtualFreeEx(hTargetProcess, pRemoteName, 0, Win32Constants.MEM_RELEASE);
                Win32Api.VirtualFreeEx(hTargetProcess, pRemoteValue, 0, Win32Constants.MEM_RELEASE);
                Win32Api.VirtualFreeEx(hTargetProcess, pRemoteData, 0, Win32Constants.MEM_RELEASE);
                Win32Api.VirtualFreeEx(hTargetProcess, pRemoteShellcode, 0, Win32Constants.MEM_RELEASE);
            }
            
            // TODO: Write process environment variables to log for debug
        }
        
        private static IntPtr OpenProcess(uint pid)
        {
            var hProcess = Win32Api.OpenProcess(
                Win32Constants.PROCESS_CREATE_THREAD | Win32Constants.PROCESS_VM_READ | Win32Constants.PROCESS_VM_WRITE
                | Win32Constants.PROCESS_VM_OPERATION | Win32Constants.PROCESS_QUERY_INFORMATION,
                false,
                pid
            );
            
            if (hProcess == IntPtr.Zero || hProcess == Win32Constants.INVALID_HANDLE_VALUE)
            {
                var err = Marshal.GetLastWin32Error();
                throw new Exception($"[{pid}] Opening the process failed: 0x{err:X}");
            }

            return hProcess;
        }

        private static TargetAddresses GetTargetAddresses(bool is32Bit)
        {
            if (is32Bit)
            {
                var helperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "x32Helper.exe");
                var psi = new ProcessStartInfo
                {
                    FileName = helperPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();

                    if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                        throw new Exception($"x32Helper failed with exit code {proc.ExitCode}");

                    var parts = output.Trim().Split(';');
                    if (parts.Length < 2)
                        throw new Exception("x32Helper returned invalid data format.");

                    return new TargetAddresses
                    {
                        LoadLibraryW = new IntPtr(int.Parse(parts[0])),
                        SetEnvironmentVariableW = new IntPtr(int.Parse(parts[1]))
                    };
                }
            }

            var hKernel32 = Win32Api.GetModuleHandle("kernel32.dll");
            var pLoadLib = Win32Api.GetProcAddress(hKernel32, "LoadLibraryW");
            var pSetEnv = Win32Api.GetProcAddress(hKernel32, "SetEnvironmentVariableW");

            if (pLoadLib == IntPtr.Zero || pSetEnv == IntPtr.Zero)
            {
                throw new Exception("Failed to resolve addresses in 64-bit kernel32.dll");
            }
            return new TargetAddresses
            {
                LoadLibraryW = pLoadLib,
                SetEnvironmentVariableW = pSetEnv
            };
        }

        static IntPtr WriteIntoProcess(IntPtr hProcess, byte[] bytesToWrite, uint bytesSize, uint protect)
        {
            var remoteAddress = Win32Api.VirtualAllocEx(hProcess, IntPtr.Zero, bytesSize + 1, Win32Constants.MEM_COMMIT | Win32Constants.MEM_RESERVE, protect);
            if (remoteAddress == IntPtr.Zero)
            {
                Win32Api.VirtualFreeEx(hProcess, remoteAddress, bytesSize, Win32Constants.MEM_RELEASE);
            
                var err = Marshal.GetLastWin32Error();
                throw new Exception($"VirtualAllocEx error: 0x{err:X}");
            }

            var dummy = new IntPtr();
            if (!Win32Api.WriteProcessMemory(hProcess, remoteAddress, bytesToWrite, bytesSize, out dummy))
            {
                Win32Api.VirtualFreeEx(hProcess, remoteAddress, bytesSize, Win32Constants.MEM_RELEASE);
            
                var err = Marshal.GetLastWin32Error();
                throw new Exception($"WriteProcessMemory error: 0x{err:X}");
            }
            
            return remoteAddress;
        }

        private static IntPtr SearchModuleByPath(IntPtr hProcess, string searchedName)
        {
            const uint hModsMax = 0x1000;
            var hMods = new IntPtr[hModsMax];

            var modulesCount = EnumModules(hProcess, hMods, hModsMax, Win32Constants.LIST_MODULES_ALL);

            Logger.WriteLine($"[{Win32Api.GetProcessId(hProcess)}] Module list ({modulesCount}):");

            var hNeededModule = IntPtr.Zero;
            for (var i = 0; i < modulesCount; i++)
            {
                var hMod = hMods[i];
                if (hMod == IntPtr.Zero || hMod == Win32Constants.INVALID_HANDLE_VALUE)
                {
                    break;
                }

                var sb = new StringBuilder((int)Win32Constants.MAX_PATH);
                if (Win32Api.GetModuleFileNameExW(hProcess, hMod, sb, Win32Constants.MAX_PATH) == 0)
                {
                    continue;
                }
                
                var modulePath = sb.ToString();
                if (string.Equals(modulePath, searchedName, StringComparison.CurrentCultureIgnoreCase))
                {
                    Logger.WriteLine($"{i + 1}. [*] {modulePath}");
                    hNeededModule = hMod;
                }
                else
                {
                    Logger.WriteLine($"{i + 1}. {modulePath}");
                }
            }

            return hNeededModule;
        }

        static uint EnumModules(IntPtr hProcess, IntPtr[] hMods, uint hModsMax, uint filters)
        {
            uint cbNeeded;
            if (!Win32Api.EnumProcessModulesEx(hProcess, hMods, hModsMax, out cbNeeded, filters))
            {
                throw new Exception($"[Handler {hProcess}] EnumProcessModulesEx failed: 0x{Marshal.GetLastWin32Error():X}");
            }

            var modulesCount = cbNeeded / (uint)Marshal.SizeOf(typeof(IntPtr));

            return modulesCount;
        }
    }
}
