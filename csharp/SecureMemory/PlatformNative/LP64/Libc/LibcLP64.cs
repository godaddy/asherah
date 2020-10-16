using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

// Configure types for LP64
using off_t = System.Int64;
using size_t = System.UInt64;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming - This file tries to mirror native code as much as possible
// ReSharper disable BuiltInTypeReferenceStyle
namespace GoDaddy.Asherah.PlatformNative.LP64.Libc
{
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1121:UseBuiltInTypeAlias", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "Matching native conventions")]
    internal static class LibcLP64
    {
        [DllImport("libc", EntryPoint = "memcpy", SetLastError = true)]
        private static extern IntPtr _memcpy(IntPtr dest, IntPtr src, size_t len);

        public static IntPtr memcpy(IntPtr dest, IntPtr src, size_t len)
        {
            return _memcpy(dest, src, len);
        }

        // **********************************************************************************************
        // madvise
        // int madvise(void *addr, size_t length, int advice);
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "madvise", SetLastError = true)]
        private static extern int _madvise(IntPtr addr, size_t length, int advice);

        public static int madvise(IntPtr addr, size_t length, int advice)
        {
            return _madvise(addr, length, advice);
        }

        // **********************************************************************************************
        // mlock
        // int mlock(const void *addr, size_t len);
        // http://man7.org/linux/man-pages/man2/mlock.2.html
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "mlock", SetLastError = true)]
        private static extern int _mlock(IntPtr start, size_t len);

        public static int mlock(IntPtr start, size_t len)
        {
            return _mlock(start, len);
        }

        // **********************************************************************************************
        // munlock
        // int munlock(const void *addr, size_t len);
        // http://man7.org/linux/man-pages/man2/mlock.2.html
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "munlock", SetLastError = true)]
        private static extern int _munlock(IntPtr start, size_t len);

        public static int munlock(IntPtr start, size_t len)
        {
            return _munlock(start, len);
        }

        // **********************************************************************************************
        // mmap
        // void *mmap(void *addr, size_t length, int prot, int flags, int fd, off_t offset);
        // http://man7.org/linux/man-pages/man2/mmap.2.html
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "mmap", SetLastError = true)]
        private static extern IntPtr _mmap(IntPtr start, size_t length, int prot, int flags, int fd, off_t offset);

        public static IntPtr mmap(IntPtr start, size_t length, int prot, int flags, int fd, off_t offset)
        {
            return _mmap(start, length, prot, flags, fd, offset);
        }

        // **********************************************************************************************
        // munmap
        // int munmap(void *addr, size_t length);
        // http://man7.org/linux/man-pages/man2/mmap.2.html
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "munmap", SetLastError = true)]
        private static extern int _munmap(IntPtr start, size_t length);

        public static int munmap(IntPtr start, size_t length)
        {
            return _munmap(start, length);
        }

        // **********************************************************************************************
        // mprotect
        // int mprotect(void *addr, size_t len, int prot);
        // http://man7.org/linux/man-pages/man2/mprotect.2.html
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "mprotect", SetLastError = true)]
        private static extern int _mprotect(IntPtr start, size_t len, int prots);

        public static int mprotect(IntPtr start, size_t len, int prots)
        {
            return _mprotect(start, len, prots);
        }

        // **********************************************************************************************
        // setrlimit
        // int setrlimit(int resource, const struct rlimit *rlim);
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "setrlimit", SetLastError = true)]
        private static extern int _setrlimit(int resource, IntPtr rlp);

        public static int setrlimit(int resource, rlimit rlp)
        {
            // Explicit boxing
            object rlpObj = rlp;

            GCHandle handle = GCHandle.Alloc(rlpObj, GCHandleType.Pinned);
            try
            {
                IntPtr rlpPtr = handle.AddrOfPinnedObject();
                return _setrlimit(resource, rlpPtr);
            }
            finally
            {
                handle.Free();
            }
        }

        // **********************************************************************************************
        // getrlimit
        // int getrlimit(int resource, struct rlimit *rlim);
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "getrlimit", SetLastError = true)]
        private static extern int _getrlimit(int resource, IntPtr rlp);

        public static int getrlimit(int resource, out rlimit rlim)
        {
            var output = default(rlimit);
            rlim = output;

            // Explicit boxing
            object rlpObj = output;

            GCHandle handle = GCHandle.Alloc(rlpObj, GCHandleType.Pinned);
            try
            {
                IntPtr rlpPtr = handle.AddrOfPinnedObject();
                int result = _getrlimit(resource, rlpPtr);
                rlim = (rlimit)rlpObj;
                return result;
            }
            finally
            {
                handle.Free();
            }
        }

        // **********************************************************************************************
        // bzero - NOTE: Linux only!
        // void bzero(void *s, size_t n);
        // http://man7.org/linux/man-pages/man3/bzero.3.html
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "bzero", SetLastError = true)]
        private static extern int _bzero(IntPtr s, size_t n);

        public static void bzero(IntPtr start, size_t len)
        {
            _bzero(start, len);
        }

        // **********************************************************************************************
        // memset_s - NOTE: MacOS only!
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
