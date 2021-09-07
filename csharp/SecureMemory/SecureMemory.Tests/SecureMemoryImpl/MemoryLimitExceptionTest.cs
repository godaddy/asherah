using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class MemoryLimitExceptionTest
    {
        private const string Message = "Failure message";

        [Fact]
        private void MemoryLimitExceptionConstructorTest()
        {
            MemoryLimitException exception = new MemoryLimitException(Message);
            Assert.Equal(Message, exception.Message);
        }
    }
}
