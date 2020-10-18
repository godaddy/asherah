using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;

namespace GoDaddy.Asherah.PlatformNative.LLP64.Windows
{
    internal class WindowsInterop
    {
        [DllImport("kernel32.dll", SetLastError=true, ExactSpelling=true)]
        public static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern bool VirtualLock(IntPtr lpAddress, UIntPtr dwSize);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern bool VirtualUnlock(IntPtr lpAddress, UIntPtr dwSize);

        [DllImport("kernel32.dll", SetLastError=true, ExactSpelling=true)]
        public static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, AllocationType dwFreeType);

        [DllImport("kernel32.dll", SetLastError=true, ExactSpelling=true)]
        public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError=true, ExactSpelling=true)]
        public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetProcessWorkingSetSize(
            IntPtr hProcess,
            ref UIntPtr lpMinimumWorkingSetSize,
            ref UIntPtr lpMaximumWorkingSetSize);

        [DllImport("crypt32.dll", SetLastError=true)]
        public static extern bool CryptProtectMemory(IntPtr ptr, UIntPtr dwSize, CryptProtectMemoryOptions dwFlags);

        [DllImport("crypt32.dll", SetLastError=true)]
        public static extern bool CryptUnprotectMemory(IntPtr ptr, UIntPtr dwSize, CryptProtectMemoryOptions dwFlags);

        [DllImport("Kernel32.dll", EntryPoint = "RtlZeroMemory", SetLastError = true, ExactSpelling = true)]
        internal static extern void ZeroMemory(IntPtr dest, UIntPtr size);

        [DllImport("kernel32.dll", EntryPoint = "RtlCopyMemory", SetLastError = false)]
        internal static extern void CopyMemory(IntPtr dest, IntPtr src, UIntPtr count);

        [DllImport("Kernel32.dll")]
        internal static extern IntPtr LoadLibrary(string path);
    }
}
