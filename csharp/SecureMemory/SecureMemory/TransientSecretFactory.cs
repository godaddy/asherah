using System;
using System.Diagnostics;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory
{
    public class TransientSecretFactory : ISecretFactory
    {
        private readonly ISecretFactory secretFactory;

        public TransientSecretFactory(IConfiguration configuration)
        {
            Debug.WriteLine("TransientSecretFactory: New");
            secretFactory = new ProtectedMemorySecretFactory(configuration);
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
