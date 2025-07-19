using System;
using System.Runtime.InteropServices;

// Configure types for LP64
using off_t = System.Int64;
using size_t = System.UInt64;

namespace GoDaddy.Asherah.PlatformNative.LP64.Libc
{
    public static class LibcLP64
    {
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

            var handle = GCHandle.Alloc(rlpObj, GCHandleType.Pinned);
            try
            {
                var rlpPtr = handle.AddrOfPinnedObject();
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

            var handle = GCHandle.Alloc(rlpObj, GCHandleType.Pinned);
            try
            {
                var rlpPtr = handle.AddrOfPinnedObject();
                var result = _getrlimit(resource, rlpPtr);
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
