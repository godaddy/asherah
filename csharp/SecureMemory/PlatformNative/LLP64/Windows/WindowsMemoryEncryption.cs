using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;

namespace GoDaddy.Asherah.PlatformNative.LLP64.Windows
{
    public class WindowsMemoryEncryption : IMemoryEncryption
    {
        public ulong GetEncryptedMemoryBlockSize()
        {
            return CryptProtect.BLOCKSIZE;
        }

        public void ProcessEncryptMemory(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.CryptProtectMemory(pointer, (UIntPtr)length, CryptProtectMemoryOptions.SAME_PROCESS))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new WindowsOperationFailedException("CryptProtectMemory", 0L, errno);
            }
        }

        public void ProcessDecryptMemory(IntPtr pointer, ulong length)
        {
            if (!WindowsInterop.CryptUnprotectMemory(pointer, (UIntPtr)length, CryptProtectMemoryOptions.SAME_PROCESS))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new WindowsOperationFailedException("CryptUnprotectMemory", 0L, errno);
            }
        }
    }
}
