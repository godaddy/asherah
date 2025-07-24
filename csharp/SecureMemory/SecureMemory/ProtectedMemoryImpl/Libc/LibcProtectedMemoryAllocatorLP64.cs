using System;
using System.Runtime.CompilerServices;
using System.Threading;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.SecureMemory.Libc;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc
{
    internal abstract class LibcProtectedMemoryAllocatorLP64 : LibcMemoryAllocatorLP64
    {
        private static long resourceLimit;
        private static long memoryLocked;

        protected LibcProtectedMemoryAllocatorLP64()
        {
            var rlim = GetMemlockResourceLimit();
            if (rlim == rlimit.UNLIMITED || rlim > long.MaxValue)
            {
                resourceLimit = long.MaxValue;
            }
            else
            {
                resourceLimit = (long)rlim;
            }
        }

        // ************************************
        // alloc / free
        // ************************************
        public override IntPtr Alloc(ulong length)
        {
            if (Interlocked.Read(ref memoryLocked) + (long)length > resourceLimit)
            {
                throw new MemoryLimitException(
                    $"Requested MemLock length exceeds resource limit max of {resourceLimit}");
            }

            // Some platforms may require fd to be -1 even if using anonymous
            var protectedMemory = LibcLP64.mmap(
                IntPtr.Zero, length, GetProtReadWrite(), GetPrivateAnonymousFlags(), -1, 0);

            Check.ValidatePointer(protectedMemory, "mmap");
            try
            {
                Check.Zero(LibcLP64.mlock(protectedMemory, length), "mlock");

                try
                {
                    Interlocked.Add(ref memoryLocked, (long)length);
                    SetNoDump(protectedMemory, length);
                }
                catch (Exception e)
                {
                    Check.Zero(LibcLP64.munlock(protectedMemory, length), "munlock", e);
                    Interlocked.Add(ref memoryLocked, 0 - (long)length);
                    throw new SecureMemoryAllocationFailedException("Failed to set no dump on protected memory", e);
                }
            }
            catch (Exception e)
            {
                Check.Zero(LibcLP64.munmap(protectedMemory, length), "munmap", e);
                throw;
            }

            return protectedMemory;
        }

        public override void Free(IntPtr pointer, ulong length)
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
                    Check.Zero(LibcLP64.munlock(pointer, length), "munlock");
                    Interlocked.Add(ref memoryLocked, 0 - (long)length);
                }
                finally
                {
                    // Regardless of whether or not we successfully unlock, unmap

                    // Free (unmap) the protected memory
                    Check.Zero(LibcLP64.munmap(pointer, length), "munmap");
                }
            }
        }

        internal abstract int GetMemLockLimit();

        internal virtual ulong GetMemlockResourceLimit()
        {
            LibcLP64.getrlimit(GetMemLockLimit(), out var rlim);
            return rlim.rlim_max;
        }
    }
}
