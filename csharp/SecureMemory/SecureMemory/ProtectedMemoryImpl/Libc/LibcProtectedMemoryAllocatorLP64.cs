using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc
{
    internal abstract class LibcProtectedMemoryAllocatorLP64 : IProtectedMemoryAllocator
    {
        private static readonly IntPtr InvalidPointer = new IntPtr(-1);

        private readonly LibcLP64 libc;

        private bool globallyDisabledCoreDumps = false;

        protected LibcProtectedMemoryAllocatorLP64(LibcLP64 libc)
        {
            if (libc == null)
            {
                throw new ArgumentNullException(nameof(libc));
            }

            this.libc = libc;
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
            libc.getrlimit(GetMemLockLimit(), out var rlim);
            if (rlim.rlim_max != rlimit.UNLIMITED && rlim.rlim_max < length)
            {
                throw new MemoryLimitException(
                    $"Requested MemLock length exceeds resource limit max of {rlim.rlim_max}");
            }

            // Some platforms may require fd to be -1 even if using anonymous
            IntPtr protectedMemory = libc.mmap(
                IntPtr.Zero, length, GetProtReadWrite(), GetPrivateAnonymousFlags(), -1, 0);

            Check.IntPtr(protectedMemory, "mmap");
            try
            {
                Check.Zero(libc.mlock(protectedMemory, length), "mlock");

                try
                {
                    SetNoDump(protectedMemory, length);
                }
                catch (Exception e)
                {
                    Check.Zero(libc.munlock(protectedMemory, length), "munlock", e);
                    throw;
                }
            }
            catch (Exception e)
            {
                Check.Zero(libc.munmap(protectedMemory, length), "munmap", e);
                throw;
            }

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
                    // Regardless of whether or not we successfully wipe, unlock

                    // Unlock the protected memory
                    Check.Zero(libc.munlock(pointer, length), "munlock");
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

        internal abstract int GetMemLockLimit();

        protected abstract void ZeroMemory(IntPtr pointer, ulong length);

        protected LibcLP64 GetLibc()
        {
            return libc;
        }
    }
}
