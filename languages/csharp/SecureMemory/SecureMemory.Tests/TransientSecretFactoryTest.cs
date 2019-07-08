using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    [Collection("Logger Fixture collection")]
    public class TransientSecretFactoryTest
    {
        private readonly TransientSecretFactory transientSecretFactory;

        public TransientSecretFactoryTest()
        {
            transientSecretFactory = new TransientSecretFactory();
        }

        [Fact]
        private void TestCreateSecretByteArray()
        {
            Secret secret = transientSecretFactory.CreateSecret(new byte[] { 0, 1 });
            Assert.Equal(typeof(ProtectedMemorySecret), secret.GetType());
        }

        [Fact]
        private void TestCreateSecretCharArray()
        {
            Secret secret = transientSecretFactory.CreateSecret(new[] { 'a', 'b' });
            Assert.Equal(typeof(ProtectedMemorySecret), secret.GetType());
        }
    }
}
