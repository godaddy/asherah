using System;
using System.Diagnostics;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;

namespace GoDaddy.Asherah.SecureMemory
{
    public class TransientSecretFactory : ISecretFactory
    {
        private readonly ISecretFactory secretFactory;

        public TransientSecretFactory()
        {
            Debug.WriteLine("TransientSecretFactory: New");
            secretFactory = new ProtectedMemorySecretFactory();
        }

        public void Dispose()
        {
            Debug.WriteLine("TransientSecretFactory: Dispose");
            secretFactory.Dispose();
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
