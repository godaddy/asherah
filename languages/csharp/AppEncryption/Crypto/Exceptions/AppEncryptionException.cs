using System;

namespace GoDaddy.Asherah.Crypto.Exceptions
{
    public class AppEncryptionException : SystemException
    {
        public AppEncryptionException(string message)
            : base(message)
        {
        }

        public AppEncryptionException(string message, Exception cause)
            : base(message, cause)
        {
        }
    }
}
