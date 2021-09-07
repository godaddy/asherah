using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class ProtectedMemoryAllocationFailedTest
    {
        private const string Message = "Failure message";

        [Fact]
        private void ProtectedMemoryAllocationFailedConstructorTest()
        {
            SecureMemoryAllocationFailedException exception = new SecureMemoryAllocationFailedException(Message);
            Assert.Equal(Message, exception.Message);
        }
    }
}
