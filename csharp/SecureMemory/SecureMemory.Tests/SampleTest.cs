using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    [Collection("Logger Fixture collection")]
    public class SampleTest
    {
        [Fact]
        private void EndToEndTest()
        {
            ISecretFactory secretFactory = new ProtectedMemorySecretFactory();
            var secretBytes = new byte[] { 0, 1, 2, 3 };
            var secret = secretFactory.CreateSecret(secretBytes.Clone() as byte[]);
            secret.WithSecretBytes(decryptedBytes => Assert.Equal(secretBytes, decryptedBytes));
        }
    }
}
