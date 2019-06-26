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
    public class LibcLP64
    {
        // **********************************************************************************************
        // madvise
        // int madvise(void *addr, size_t length, int advice);
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "madvise", SetLastError = true)]
        private static extern int _madvise(IntPtr addr, size_t length, int advice);

        public int madvise(IntPtr addr, size_t length, int advice)
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

        public int mlock(IntPtr start, size_t len)
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

        public int munlock(IntPtr start, size_t len)
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

        public IntPtr mmap(IntPtr start, size_t length, int prot, int flags, int fd, off_t offset)
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

        public int munmap(IntPtr start, size_t length)
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

        public int mprotect(IntPtr start, size_t len, int prots)
        {
            return _mprotect(start, len, prots);
        }

        // **********************************************************************************************
        // setrlimit
        // int setrlimit(int resource, const struct rlimit *rlim);
        // **********************************************************************************************
        [DllImport("libc", EntryPoint = "setrlimit", SetLastError = true)]
        private static extern int _setrlimit(int resource, IntPtr rlp);

        public int setrlimit(int resource, rlimit rlp)
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

        public int getrlimit(int resource, out rlimit rlim)
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
    }
}
