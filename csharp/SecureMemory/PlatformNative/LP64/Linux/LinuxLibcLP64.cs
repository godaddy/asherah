using System;
using System.Runtime.InteropServices;

// Configure types for LP64
using size_t = System.UInt64;

namespace GoDaddy.Asherah.PlatformNative.LP64.Linux
{
    public static class LinuxLibcLP64
    {
        // **********************************************************************************************
        // bzero
        // void bzero(void *s, size_t n);
        // http://man7.org/linux/man-pages/man3/bzero.3.html
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "bzero", SetLastError = true)]
        private static extern int _bzero(IntPtr s, size_t n);

        public static void bzero(IntPtr start, size_t len)
        {
            // On Linux/glibc bzero is guaranteed to not be optimized away?
            // NOTE: there was a note in the java repo that glibc didn't have explicit_bzero?
            _bzero(start, len);
        }

        [DllImport("libc", EntryPoint = "memcpy", SetLastError = true)]
        private static extern IntPtr _memcpy(IntPtr dest, IntPtr src, size_t len);

        public static IntPtr memcpy(IntPtr dest, IntPtr src, size_t len)
        {
            return _memcpy(dest, src, len);
        }
    }
}
