using System;

namespace GoDaddy.Asherah.PlatformNative
{
    public interface IMemoryEncryption
    {
        ulong GetEncryptedMemoryBlockSize();

        void ProcessEncryptMemory(IntPtr pointer, ulong length);

        void ProcessDecryptMemory(IntPtr pointer, ulong length);
    }
}
