using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;

// Configure types for LP64
using size_t = System.UInt64;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming - This file tries to mirror native code as much as possible
namespace GoDaddy.Asherah.PlatformNative.LP64.Linux
{
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1121:UseBuiltInTypeAlias", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "Matching native conventions")]
    public class LinuxLibcLP64 : LibcLP64
    {
        // **********************************************************************************************
        // bzero
        // void bzero(void *s, size_t n);
        // http://man7.org/linux/man-pages/man3/bzero.3.html
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "bzero", SetLastError = true)]
        private static extern int _bzero(IntPtr s, size_t n);

        public void bzero(IntPtr start, size_t len)
        {
            // On Linux/glibc bzero is guaranteed to not be optimized away?
            // NOTE: there was a note in the java repo that glibc didn't have explicit_bzero?
            _bzero(start, len);
        }

        [DllImport("libc", EntryPoint = "memcpy", SetLastError = true)]
        private static extern IntPtr _memcpy(IntPtr dest, IntPtr src, size_t len);

        public IntPtr memcpy(IntPtr dest, IntPtr src, size_t len)
        {
            return _memcpy(dest, src, len);
        }
    }
}
