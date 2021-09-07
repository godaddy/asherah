using System;

namespace GoDaddy.Asherah.SecureMemory.SecureMemoryImpl
{
    public class SecureMemoryException : SystemException
    {
        public SecureMemoryException(string message)
            : base(message)
        {
        }
    }
}
