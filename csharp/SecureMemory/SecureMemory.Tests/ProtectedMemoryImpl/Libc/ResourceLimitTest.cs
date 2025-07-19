using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using System.Runtime.InteropServices;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Libc
{
    [Collection("Logger Fixture collection")]
    public class ResourceLimitTest
    {
        [Fact]
        private void TestZero()
        {
            Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Test only runs on Linux or macOS");

            var zeroRlimit = rlimit.Zero();
            Assert.Equal(0UL, zeroRlimit.rlim_cur);
            Assert.Equal(0UL, zeroRlimit.rlim_max);
        }
    }
}
