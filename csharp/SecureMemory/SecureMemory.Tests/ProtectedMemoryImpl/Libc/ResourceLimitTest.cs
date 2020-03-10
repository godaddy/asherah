using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Libc
{
    [Collection("Logger Fixture collection")]
    public class ResourceLimitTest
    {
        [Fact]
        private void TestZero()
        {
            rlimit zeroRlimit = rlimit.Zero();
            Assert.Equal(0UL, zeroRlimit.rlim_cur);
            Assert.Equal(0UL, zeroRlimit.rlim_max);
        }
    }
}
