using System;

namespace GoDaddy.Asherah.SecureMemory
{
    public interface ISecretFactory : IDisposable
    {
        Secret CreateSecret(byte[] secretData);

        Secret CreateSecret(char[] secretData);
    }
}
