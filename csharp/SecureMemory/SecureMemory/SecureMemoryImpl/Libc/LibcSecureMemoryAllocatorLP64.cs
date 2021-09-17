using System;
using System.Runtime.CompilerServices;
using System.Threading;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Libc
{
    internal abstract class LibcSecureMemoryAllocatorLP64 : ISecureMemoryAllocator
    {
        private static long memoryLocked;
        private readonly LibcLP64 libc;
        private bool globallyDisabledCoreDumps = false;

        protected LibcSecureMemoryAllocatorLP64(LibcLP64 libc)
        {
            this.libc = libc ?? throw new ArgumentNullException(nameof(libc));
        }

        // Implementation order of preference:
        // memset_s (standards)
        // explicit_bzero (BSD)
        // SecureZeroMemory (Windows)
        // bzero (Linux, same guarantees as explicit_bzero)
        public virtual void SetNoAccess(IntPtr pointer, ulong length)
        {
            Check.Zero(libc.mprotect(pointer, length, GetProtNoAccess()), "mprotect(PROT_NONE)");
        }

        public virtual void SetReadAccess(IntPtr pointer, ulong length)
        {
            Check.Zero(libc.mprotect(pointer, length, GetProtRead()), "mprotect(PROT_READ)");
        }

        public virtual void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            Check.Zero(libc.mprotect(pointer, length, GetProtReadWrite()), "mprotect(PROT_READ|PROT_WRITE)");
        }

        // ************************************
        // alloc / free
        // ************************************
        public virtual IntPtr Alloc(ulong length)
        {
            // Some platforms may require fd to be -1 even if using anonymous
            IntPtr protectedMemory = libc.mmap(
                IntPtr.Zero, length, GetProtReadWrite(), GetPrivateAnonymousFlags(), -1, 0);

            Check.IntPtr(protectedMemory, "mmap");
            SetNoDump(protectedMemory, length);

            return protectedMemory;
        }

        public virtual void Free(IntPtr pointer, ulong length)
        {
            try
            {
                // Wipe the protected memory (assumes memory was made writeable)
                ZeroMemory(pointer, length);
            }
            finally
            {
                try
                {
                    Interlocked.Add(ref memoryLocked, 0 - (long)length);
                }
                finally
                {
                    // Regardless of whether or not we successfully unlock, unmap

                    // Free (unmap) the protected memory
                    Check.Zero(libc.munmap(pointer, length), "munmap");
                }
            }
        }

        public abstract void Dispose();

        internal abstract int GetRlimitCoreResource();

        // ************************************
        // Core dumps
        // ************************************
        internal abstract void SetNoDump(IntPtr pointer, ulong length);

        internal bool AreCoreDumpsGloballyDisabled()
        {
            return globallyDisabledCoreDumps;
        }

        internal void DisableCoreDumpGlobally()
        {
            Check.Zero(libc.setrlimit(GetRlimitCoreResource(), rlimit.Zero()), "setrlimit(RLIMIT_CORE)");

            globallyDisabledCoreDumps = true;
        }

        // ************************************
        // Memory protection
        // ************************************
        internal abstract int GetProtRead();

        internal abstract int GetProtReadWrite();

        internal abstract int GetProtNoAccess();

        internal abstract int GetPrivateAnonymousFlags();

        protected abstract void ZeroMemory(IntPtr pointer, ulong length);

        protected LibcLP64 GetLibc()
        {
            return libc;
        }
    }
}
