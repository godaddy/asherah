using System;

namespace GoDaddy.Asherah.PlatformNative
{
    public interface IMemoryEncryption : IDisposable
    {
        void ProcessEncryptMemory(IntPtr pointer, ulong dataLength);

        void ProcessDecryptMemory(IntPtr pointer, ulong dataLength);

        // Returns the required allocation size for a data size
        // This can include block size rounding and extra space for appended nonce / iv
        int GetBufferSizeForAlloc(int dataLength);
    }
}
