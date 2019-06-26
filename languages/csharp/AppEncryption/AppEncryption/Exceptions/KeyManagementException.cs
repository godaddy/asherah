using GoDaddy.Asherah.Crypto.Exceptions;

namespace GoDaddy.Asherah.AppEncryption.Exceptions
{
    public class KeyManagementException : AppEncryptionException
    {
        public KeyManagementException(string message)
            : base(message)
        {
        }
    }
}
