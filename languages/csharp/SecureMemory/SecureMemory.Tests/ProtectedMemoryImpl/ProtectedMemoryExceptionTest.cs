using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class ProtectedMemoryExceptionTest
    {
        private const string Message = "Failure message";

        [Fact]
        private void ProtectedMemoryExceptionConstructorTest()
        {
            ProtectedMemoryException exception = new ProtectedMemoryException(Message);
            Assert.Equal(Message, exception.Message);
        }
    }
}
