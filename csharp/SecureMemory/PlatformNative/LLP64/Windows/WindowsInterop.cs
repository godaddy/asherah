using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;

namespace GoDaddy.Asherah.PlatformNative.LLP64.Windows
{
    public class WindowsInterop
    {
        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern IntPtr HeapCreate(uint flOptions, UIntPtr dwInitialSize, UIntPtr dwMaximumSize);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern bool HeapDestroy(IntPtr hHeap);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern IntPtr GlobalFree(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern IntPtr HeapAlloc(IntPtr hHeap, uint dwFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern bool HeapFree(IntPtr hHeap, uint dwFlags, IntPtr lpMem);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("Kernel32.dll", EntryPoint="RtlZeroMemory", SetLastError=true)]
        public static extern void ZeroMemory(IntPtr dest, IntPtr size);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError=true, ExactSpelling=true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [DllImport("kernel32.dll", SetLastError=true, ExactSpelling=true)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, AllocationType dwFreeType);
    }
}
