namespace GoDaddy.Asherah.PlatformNative
{
    public abstract class CryptProtectMemory
    {
        protected ulong RoundToBlockSize(ulong length, ulong blockSize)
        {
            return (length + (blockSize - 1)) & ~(blockSize - 1);
        }
    }
}
