using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;

namespace GoDaddy.Asherah.PlatformNative.LLP64.Windows
{
    public class WindowsInterop
    {
        [DllImport("Kernel32.dll", EntryPoint="RtlZeroMemory", SetLastError=true, ExactSpelling=true)]
        public static extern void ZeroMemory(IntPtr dest, UIntPtr size);

        [DllImport("kernel32.dll", SetLastError=true, ExactSpelling=true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError=true, ExactSpelling=true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [DllImport("kernel32.dll", SetLastError=true, ExactSpelling=true)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, AllocationType dwFreeType);

        [DllImport("crypt32.dll", SetLastError = true)]
        public static extern bool CryptProtectMemory(IntPtr ptr, UIntPtr dwSize, CryptProtectMemoryOptions dwFlags);

        [DllImport("crypt32.dll", SetLastError = true)]
        public static extern bool CryptUnprotectMemory(IntPtr ptr, UIntPtr dwSize, CryptProtectMemoryOptions dwFlags);
    }
}
