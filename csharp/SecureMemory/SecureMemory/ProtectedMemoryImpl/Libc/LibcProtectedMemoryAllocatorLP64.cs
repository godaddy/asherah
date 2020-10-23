using System;
using System.Threading;
using GoDaddy.Asherah.PlatformNative;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc
{
    internal class LibcProtectedMemoryAllocatorLP64 : IProtectedMemoryAllocator
    {
        private static ulong resourceLimit;
        private static long memoryLocked;

        public LibcProtectedMemoryAllocatorLP64(SystemInterface systemInterface)
        {
            SystemInterface = systemInterface ?? throw new ArgumentNullException(nameof(systemInterface));
            resourceLimit = systemInterface.GetMemoryLockLimit();
        }

        protected SystemInterface SystemInterface { get; }

        public virtual void SetNoAccess(IntPtr pointer, ulong length)
        {
            SystemInterface.SetNoAccess(pointer, length);
        }

        public virtual void SetReadAccess(IntPtr pointer, ulong length)
        {
            SystemInterface.SetReadAccess(pointer, length);
        }

        public virtual void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            SystemInterface.SetReadWriteAccess(pointer, length);
        }

        public virtual IntPtr Alloc(ulong length)
        {
            if ((ulong)Interlocked.Read(ref memoryLocked) + length > resourceLimit)
            {
                throw new MemoryLimitException(
                    $"Requested MemLock length exceeds resource limit max of {resourceLimit}");
            }

            IntPtr protectedMemory = SystemInterface.PageAlloc(length);

            Check.IntPtr(protectedMemory, "PageAlloc");
            try
            {
                SystemInterface.LockMemory(protectedMemory, length);

                try
                {
                    Interlocked.Add(ref memoryLocked, (long)length);
                    SystemInterface.SetNoDump(protectedMemory, length);
                }
                catch (Exception exception)
                {
                    try
                    {
                        SystemInterface.UnlockMemory(protectedMemory, length);
                        Interlocked.Add(ref memoryLocked, 0 - (long)length);
                    }
                    catch (Exception unlockException)
                    {
                        throw new AggregateException(exception, unlockException);
                    }

                    throw;
                }
            }
            catch (Exception exception)
            {
                try
                {
                    SystemInterface.PageFree(protectedMemory, length);
                }
                catch (Exception pageFreeException)
                {
                    throw new AggregateException(exception, pageFreeException);
                }

                throw;
            }

            return protectedMemory;
        }

        public virtual void Free(IntPtr pointer, ulong length)
        {
            try
            {
                // Make memory writable if needed
                SetReadWriteAccess(pointer, length);

                // Wipe the protected memory
                SystemInterface.ZeroMemory(pointer, length);
            }
            finally
            {
                try
                {
                    // Regardless of whether or not we successfully wipe, unlock

                    // Unlock the protected memory
                    SystemInterface.UnlockMemory(pointer, length);
                    Interlocked.Add(ref memoryLocked, 0 - (long)length);
                }
                finally
                {
                    // Regardless of whether or not we successfully unlock, unmap

                    // Free (unmap) the protected memory
                    SystemInterface.PageFree(pointer, length);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }
    }
}
