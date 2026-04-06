using System;

namespace DllInjector.Models
{
    public struct TargetAddresses
    {
        public IntPtr LoadLibraryW;
        public IntPtr SetEnvironmentVariableW;
    }
}