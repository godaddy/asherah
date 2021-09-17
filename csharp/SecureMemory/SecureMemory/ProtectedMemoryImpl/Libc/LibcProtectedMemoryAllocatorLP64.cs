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

        protected LibcProtectedMemoryAllocatorLP64(LibcLP64 libc)
            : base(libc)
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
            IntPtr protectedMemory = Libc.mmap(
                IntPtr.Zero, length, GetProtReadWrite(), GetPrivateAnonymousFlags(), -1, 0);

            Check.IntPtr(protectedMemory, "mmap");
            try
            {
                Check.Zero(Libc.mlock(protectedMemory, length), "mlock");

                try
                {
                    Interlocked.Add(ref memoryLocked, (long)length);
                    SetNoDump(protectedMemory, length);
                }
                catch (Exception e)
                {
                    Check.Zero(Libc.munlock(protectedMemory, length), "munlock", e);
                    Interlocked.Add(ref memoryLocked, 0 - (long)length);
                    throw;
                }
            }
            catch (Exception e)
            {
                Check.Zero(Libc.munmap(protectedMemory, length), "munmap", e);
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
                    Check.Zero(Libc.munlock(pointer, length), "munlock");
                    Interlocked.Add(ref memoryLocked, 0 - (long)length);
                }
                finally
                {
                    // Regardless of whether or not we successfully unlock, unmap

                    // Free (unmap) the protected memory
                    Check.Zero(Libc.munmap(pointer, length), "munmap");
                }
            }
        }

        internal abstract int GetMemLockLimit();

        internal virtual ulong GetMemlockResourceLimit()
        {
            Libc.getrlimit(GetMemLockLimit(), out var rlim);
            return rlim.rlim_max;
        }
    }
}
