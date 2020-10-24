using System;
using GoDaddy.Asherah.PlatformNative;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows
{
    internal class WindowsProtectedMemoryAllocatorLLP64 : IProtectedMemoryAllocator
    {
        private const int DefaultMaximumWorkingSetSize = 67108860;

        // private const int DefaultMinimumWorkingSetSize = 33554430;
        private readonly SystemInterface systemInterface;
        private readonly IMemoryEncryption memoryEncryption;

        public WindowsProtectedMemoryAllocatorLLP64(IConfiguration configuration, SystemInterface systemInterface, IMemoryEncryption memoryEncryption)
        {
            this.systemInterface = systemInterface ?? throw new ArgumentNullException(nameof(systemInterface));
            this.memoryEncryption = memoryEncryption;

            /*
            ulong min = 0;

            var minConfig = configuration["minimumWorkingSetSize"];
            if (!string.IsNullOrWhiteSpace(minConfig))
            {
                min = ulong.Parse(minConfig);
            }
            else
            {
                if (min < DefaultMinimumWorkingSetSize)
                {
                    min = DefaultMinimumWorkingSetSize;
                }
            }
            */

            ulong max = 0;
            var maxConfig = configuration["maximumWorkingSetSize"];
            if (!string.IsNullOrWhiteSpace(maxConfig))
            {
                max = ulong.Parse(maxConfig);
            }
            else
            {
                if (max < DefaultMaximumWorkingSetSize)
                {
                    max = DefaultMaximumWorkingSetSize;
                }
            }

            systemInterface.SetMemoryLockLimit(max);
        }

        public virtual IntPtr Alloc(ulong length)
        {
            length = (ulong)memoryEncryption.GetBufferSizeForAlloc((int)length);
            return systemInterface.PageAlloc(length);
        }

        public virtual void Free(IntPtr pointer, ulong length)
        {
            length = (ulong)memoryEncryption.GetBufferSizeForAlloc((int)length);
            systemInterface.ZeroMemory(pointer, length);
            systemInterface.PageFree(pointer, length);
        }

        public void SetNoAccess(IntPtr pointer, ulong length)
        {
            memoryEncryption.ProcessEncryptMemory(pointer, length);
            systemInterface.UnlockMemory(pointer, length);
        }

        public void SetReadAccess(IntPtr pointer, ulong length)
        {
            systemInterface.LockMemory(pointer, length);
            memoryEncryption.ProcessDecryptMemory(pointer, length);
        }

        public void SetReadWriteAccess(IntPtr pointer, ulong length)
        {
            systemInterface.LockMemory(pointer, length);
            memoryEncryption.ProcessDecryptMemory(pointer, length);
        }

        public void Dispose()
        {
        }
    }
}
