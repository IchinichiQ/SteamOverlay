using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DllInjector
{
    internal class Win32Constants
    {
        public const uint CREATE_SUSPENDED = 0x00000004;
        public const uint CREATE_NEW_CONSOLE = 0x00000010;
        public const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        public const uint PROCESS_CREATE_THREAD = 0x0002;
        public const uint PROCESS_VM_READ = 0x0010;
        public const uint PROCESS_VM_WRITE = 0x0020;
        public const uint PROCESS_VM_OPERATION = 0x0008;
        public const uint PROCESS_QUERY_INFORMATION = 0x0400;
        public const uint ERROR_INVALID_PARAMETER = 0x00000057;
        public const uint PAGE_READWRITE = 0x04;
        public const uint WAIT_FAILED = 0xFFFFFFFF;
        public const uint WAIT_OBJECT_0 = 0;
        public const uint MEM_COMMIT = 0x00001000;
        public const uint MEM_RESERVE = 0x00002000;
        public const uint LIST_MODULES_ALL = 0x03;
        public const uint MAX_PATH = 260;
        public const uint MEM_FREE = 0x00010000;
        public const uint STARTF_USESHOWWINDOW = 0x00000001;
        public const uint SW_SHOW = 5;
        public const int ERROR_ELEVATION_REQUIRED = 0x2E4;
    }
}
