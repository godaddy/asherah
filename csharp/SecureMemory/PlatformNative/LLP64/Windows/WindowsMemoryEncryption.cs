using System;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums;

namespace GoDaddy.Asherah.PlatformNative.LLP64.Windows
{
    public class WindowsMemoryEncryption : CryptProtectMemory, IMemoryEncryption
    {
        public void ProcessEncryptMemory(IntPtr pointer, ulong dataLength)
        {
            // CryptProtectMemory expects the dataLength to be a multiple of blocksize
            dataLength = (ulong)GetBufferSizeForAlloc((int)dataLength);
            if (!WindowsInterop.CryptProtectMemory(pointer, (UIntPtr)dataLength, CryptProtectMemoryOptions.SAME_PROCESS))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new WindowsOperationFailedException("CryptProtectMemory", 0L, errno);
            }
        }

        public void ProcessDecryptMemory(IntPtr pointer, ulong dataLength)
        {
            // CryptUnprotectMemory expects the dataLength to be a multiple of blocksize
            dataLength = (ulong)GetBufferSizeForAlloc((int)dataLength);
            if (!WindowsInterop.CryptUnprotectMemory(pointer, (UIntPtr)dataLength, CryptProtectMemoryOptions.SAME_PROCESS))
            {
                var errno = Marshal.GetLastWin32Error();
                throw new WindowsOperationFailedException("CryptUnprotectMemory", 0L, errno);
            }
        }

        public int GetBufferSizeForAlloc(int dataLength)
        {
            // Windows CryptProtectMemory only needs to be rounded up to block size
            return (int)RoundToBlockSize((ulong)dataLength, CryptProtect.BLOCKSIZE);
        }

        public void Dispose()
        {
        }
    }
}
