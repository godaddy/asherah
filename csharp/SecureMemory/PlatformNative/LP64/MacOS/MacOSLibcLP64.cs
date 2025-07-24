using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

// Configure types for LP64
using size_t = System.UInt64;

namespace GoDaddy.Asherah.PlatformNative.LP64.MacOS
{
    public static class MacOSLibcLP64
    {
        // **********************************************************************************************
        // memset_s
        // errno_t memset_s( void *dest, rsize_t destsz, int ch, rsize_t count );
        // http://en.cppreference.com/w/c/string/byte/memset
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "memset_s", SetLastError = true)]
        private static extern int _memset_s(IntPtr dest, size_t destSize, int val, size_t count);

        [ExcludeFromCodeCoverage]
        public static int memset_s(IntPtr dest, size_t destSize, int val, size_t count)
        {
            return _memset_s(dest, destSize, val, count);
        }
    }
}
