using System;

namespace GoDaddy.Asherah.Crypto.Exceptions
{
    public class CipherNotSupportedException : Exception
    {
        public CipherNotSupportedException(string message)
            : base(message)
        {
        }
    }
}
