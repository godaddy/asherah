using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;

// Configure types for LP64
using size_t = System.UInt64;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming - This file tries to mirror native code as much as possible
namespace GoDaddy.Asherah.PlatformNative.LP64.MacOS
{
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1121:UseBuiltInTypeAlias", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "Matching native conventions")]
    public class MacOSLibcLP64 : LibcLP64
    {
        // **********************************************************************************************
        // memset_s
        // errno_t memset_s( void *dest, rsize_t destsz, int ch, rsize_t count );
        // http://en.cppreference.com/w/c/string/byte/memset
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "memset_s", SetLastError = true)]
        private static extern int _memset_s(IntPtr dest, size_t destSize, int val, size_t count);

        public int memset_s(IntPtr dest, size_t destSize, int val, size_t count)
        {
            return _memset_s(dest, destSize, val, count);
        }
    }
}
