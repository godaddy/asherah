using GoDaddy.Asherah.PlatformNative;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows
{
    internal sealed class WindowsProtectedMemoryAllocatorVirtualAlloc : WindowsProtectedMemoryAllocatorLLP64
    {
        private const int DefaultMaximumWorkingSetSize = 67108860;
        private const int DefaultMinimumWorkingSetSize = 33554430;

        public WindowsProtectedMemoryAllocatorVirtualAlloc(IConfiguration configuration, SystemInterface systemInterface)
            : base(systemInterface)
        {
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

            SystemInterface.SetMemoryLockLimit(max);
        }
    }
}
