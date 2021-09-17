using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using System.Runtime.InteropServices;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl.Libc
{
    [Collection("Logger Fixture collection")]
    public class ResourceLimitTest
    {
        [SkippableFact]
        private void TestZero()
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX));

            rlimit zeroRlimit = rlimit.Zero();
            Assert.Equal(0UL, zeroRlimit.rlim_cur);
            Assert.Equal(0UL, zeroRlimit.rlim_max);
        }
    }
}
