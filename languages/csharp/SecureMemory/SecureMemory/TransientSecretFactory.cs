using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;

namespace GoDaddy.Asherah.SecureMemory
{
    public class TransientSecretFactory : ISecretFactory
    {
        private readonly ISecretFactory secretFactory;

        public TransientSecretFactory()
        {
            secretFactory = new ProtectedMemorySecretFactory();
        }

        public Secret CreateSecret(byte[] secretData)
        {
            return secretFactory.CreateSecret(secretData);
        }

        public Secret CreateSecret(char[] secretData)
        {
            return secretFactory.CreateSecret(secretData);
        }
    }
}
