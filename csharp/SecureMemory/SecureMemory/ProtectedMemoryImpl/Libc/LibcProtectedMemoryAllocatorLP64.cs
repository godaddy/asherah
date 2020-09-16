using System;
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
            this.libc = libc;
        }

        // Implementation order of preference:
        // memset_s (standards)
        // explicit_bzero (BSD)
        // SecureZeroMemory (Windows)
        // bzero (Linux, same guarantees as explicit_bzero)
        public virtual void SetNoAccess(IntPtr pointer, ulong length)
        {
            CheckZero(libc.mprotect(pointer, length, GetProtNoAccess()), "mprotect(PROT_NONE)");
        }

        public virtual void SetReadAccess(IntPtr pointer, ulong length)
        {
            CheckZero(libc.mprotect(pointer, length, GetProtRead()), "mprotect(PROT_READ)");
        }

        public virtual void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            CheckZero(libc.mprotect(pointer, length, GetProtReadWrite()), "mprotect(PROT_READ|PROT_WRITE)");
        }

        public abstract void ZeroMemory(IntPtr pointer, ulong length);

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

            CheckIntPtr(protectedMemory, "mmap");
            try
            {
                CheckZero(libc.mlock(protectedMemory, length), "mlock");

                try
                {
                    SetNoDump(protectedMemory, length);
                }
                catch (Exception e)
                {
                    CheckZero(libc.munlock(protectedMemory, length), "munlock", e);
                    throw;
                }
            }
            catch (Exception e)
            {
                CheckZero(libc.munmap(protectedMemory, length), "munmap", e);
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
                    CheckZero(libc.munlock(pointer, length), "munlock");
                }
                finally
                {
                    // Regardless of whether or not we successfully unlock, unmap

                    // Free (unmap) the protected memory
                    CheckZero(libc.munmap(pointer, length), "munmap");
                }
            }
        }

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
            CheckZero(libc.setrlimit(GetRlimitCoreResource(), rlimit.Zero()), "setrlimit(RLIMIT_CORE)");

            globallyDisabledCoreDumps = true;
        }

        internal void CheckIntPtr(IntPtr intPointer, string methodName)
        {
            if (intPointer == IntPtr.Zero || intPointer == InvalidPointer)
            {
                throw new LibcOperationFailedException(methodName, intPointer.ToInt64());
            }
        }

        internal virtual void CheckZero(int result, string methodName)
        {
            if (result != 0)
            {
                // NOTE: Even though this references Win32 it actually returns
                // the last errno on non-Windows platforms.
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException(methodName, result, errno);
            }
        }

        internal void CheckZero(int result, string methodName, Exception exceptionInProgress)
        {
            if (result != 0)
            {
                throw new LibcOperationFailedException(methodName, result, exceptionInProgress);
            }
        }

        protected LibcLP64 GetLibc()
        {
            return libc;
        }

        // ************************************
        // Memory protection
        // ************************************
        protected abstract int GetProtRead();

        protected abstract int GetProtReadWrite();

        protected abstract int GetProtNoAccess();

        protected abstract int GetPrivateAnonymousFlags();

        protected abstract int GetMemLockLimit();
    }
}
