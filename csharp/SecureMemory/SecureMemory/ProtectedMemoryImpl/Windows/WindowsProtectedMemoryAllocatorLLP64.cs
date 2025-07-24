using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows
{
    internal abstract class WindowsProtectedMemoryAllocatorLLP64 : ISecureMemoryAllocator
    {
        protected static readonly IntPtr InvalidPointer = new IntPtr(-1);

        public abstract IntPtr Alloc(ulong length);

        public abstract void Free(IntPtr pointer, ulong length);

        public void SetNoAccess(IntPtr pointer, ulong length)
        {
            length = AdjustLength(length);

            if (!WindowsInterop.CryptProtectMemory(pointer, (UIntPtr)length, CryptProtectMemoryOptions.SAME_PROCESS))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new WindowsOperationFailedException("CryptProtectMemory", 0L, errno);
            }

            UnlockMemory(pointer, length);
        }

        public void SetReadAccess(IntPtr pointer, ulong length)
        {
            length = AdjustLength(length);

            LockMemory(pointer, length);

            if (!WindowsInterop.CryptUnprotectMemory(pointer, (UIntPtr)length, CryptProtectMemoryOptions.SAME_PROCESS))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new WindowsOperationFailedException("CryptUnprotectMemory", 0L, errno);
            }
        }

        public void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            length = AdjustLength(length);

            LockMemory(pointer, length);

            if (!WindowsInterop.CryptUnprotectMemory(pointer, (UIntPtr)length, CryptProtectMemoryOptions.SAME_PROCESS))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new WindowsOperationFailedException("CryptUnprotectMemory", 0L, errno);
            }
        }

        public static void ZeroMemory(IntPtr pointer, ulong length)
        {
            WindowsInterop.ZeroMemory(pointer, (UIntPtr)length);
        }

        public void Dispose()
        {
        }

        protected static void LockMemory(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.VirtualLock(pointer, (UIntPtr)length))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new WindowsOperationFailedException("VirtualLock", 0L, errno);
            }
        }

        protected static void UnlockMemory(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.VirtualUnlock(pointer, (UIntPtr)length))
            {
                var errno = Marshal.GetLastWin32Error();
                if (errno == (int)VirtualUnlockErrors.ERROR_NOT_LOCKED)
                {
                    return;
                }

                throw new WindowsOperationFailedException("VirtualUnlock", 0L, errno);
            }
        }

        protected static ulong AdjustLength(ulong length)
        {
            return length % CryptProtect.BLOCKSIZE != 0
                ? ((length / CryptProtect.BLOCKSIZE) + 1) * CryptProtect.BLOCKSIZE
                : length;
        }
    }
}
