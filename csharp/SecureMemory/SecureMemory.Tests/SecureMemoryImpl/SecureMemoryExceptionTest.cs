using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class SecureMemoryExceptionTest
    {
        private const string Message = "Failure message";

        [Fact]
        private void ProtectedMemoryExceptionConstructorTest()
        {
            SecureMemoryException exception = new SecureMemoryException(Message);
            Assert.Equal(Message, exception.Message);
        }
    }
}
