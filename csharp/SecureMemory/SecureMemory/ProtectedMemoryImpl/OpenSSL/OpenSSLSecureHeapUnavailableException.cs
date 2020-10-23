using System;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.OpenSSL
{
    public class OpenSSLSecureHeapUnavailableException : Exception
    {
        public OpenSSLSecureHeapUnavailableException(string message)
            : base(message)
        {
        }
    }
}
