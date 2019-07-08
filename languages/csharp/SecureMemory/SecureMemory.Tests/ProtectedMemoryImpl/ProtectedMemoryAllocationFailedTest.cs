using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class ProtectedMemoryAllocationFailedTest
    {
        private const string Message = "Failure message";

        [Fact]
        private void ProtectedMemoryAllocationFailedConstructorTest()
        {
            ProtectedMemoryAllocationFailedException exception = new ProtectedMemoryAllocationFailedException(Message);
            Assert.Equal(Message, exception.Message);
        }
    }
}
