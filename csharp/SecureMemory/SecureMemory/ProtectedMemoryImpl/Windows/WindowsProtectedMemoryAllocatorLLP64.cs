using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows
{
    internal abstract class WindowsProtectedMemoryAllocatorLLP64 : IProtectedMemoryAllocator
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
                throw new LibcOperationFailedException("CryptProtectMemory", 0L, errno);
            }
        }

        public void SetReadAccess(IntPtr pointer, ulong length)
        {
            length = AdjustLength(length);

            if (!WindowsInterop.CryptUnprotectMemory(pointer, (UIntPtr)length, CryptProtectMemoryOptions.SAME_PROCESS))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("CryptUnprotectMemory", 0L, errno);
            }
        }

        public void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            length = AdjustLength(length);

            if (!WindowsInterop.CryptUnprotectMemory(pointer, (UIntPtr)length, CryptProtectMemoryOptions.SAME_PROCESS))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new LibcOperationFailedException("CryptUnprotectMemory", 0L, errno);
            }
        }

        public void ZeroMemory(IntPtr pointer, ulong length)
        {
            WindowsInterop.ZeroMemory(pointer, (UIntPtr)length);
        }

        protected ulong AdjustLength(ulong length)
        {
            return length % CryptProtect.BLOCKSIZE != 0
                ? ((length / CryptProtect.BLOCKSIZE) + 1) * CryptProtect.BLOCKSIZE
                : length;
        }
    }
}
