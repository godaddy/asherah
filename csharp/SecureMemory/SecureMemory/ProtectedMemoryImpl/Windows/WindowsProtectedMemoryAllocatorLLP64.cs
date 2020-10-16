using System;
using GoDaddy.Asherah.PlatformNative;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows
{
    internal abstract class WindowsProtectedMemoryAllocatorLLP64 : IProtectedMemoryAllocator
    {
        private ulong encryptedMemoryBlockSize;

        protected WindowsProtectedMemoryAllocatorLLP64(SystemInterface systemInterface)
        {
            if (systemInterface == null)
            {
                throw new ArgumentNullException(nameof(systemInterface));
            }

            SystemInterface = systemInterface;
            encryptedMemoryBlockSize = systemInterface.GetEncryptedMemoryBlockSize();
        }

        protected SystemInterface SystemInterface { get; }

        public virtual IntPtr Alloc(ulong length)
        {
            // Adjust length to CryptProtect block size
            length = AdjustLength(length);

            return SystemInterface.PageAlloc(length);
        }

        public virtual void Free(IntPtr pointer, ulong length)
        {
            // Adjust length to CryptProtect block size
            length = AdjustLength(length);

            SystemInterface.ZeroMemory(pointer, length);
            SystemInterface.PageFree(pointer, length);
        }

        public void SetNoAccess(IntPtr pointer, ulong length)
        {
            length = AdjustLength(length);

            SystemInterface.ProcessEncryptMemory(pointer, length);
            SystemInterface.UnlockMemory(pointer, length);
        }

        public void SetReadAccess(IntPtr pointer, ulong length)
        {
            length = AdjustLength(length);

            SystemInterface.LockMemory(pointer, length);

            SystemInterface.ProcessDecryptMemory(pointer, length);
        }

        public void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            length = AdjustLength(length);

            SystemInterface.LockMemory(pointer, length);

            SystemInterface.ProcessDecryptMemory(pointer, length);
        }

        public void ZeroMemory(IntPtr pointer, ulong length)
        {
            SystemInterface.ZeroMemory(pointer, length);
        }

        public void Dispose()
        {
        }

        protected ulong AdjustLength(ulong length)
        {
            return length % encryptedMemoryBlockSize != 0
                ? ((length / encryptedMemoryBlockSize) + 1) * encryptedMemoryBlockSize
                : length;
        }
    }
}
