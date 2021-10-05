using System;

namespace GoDaddy.Asherah.SecureMemory
{
    public class SecureMemoryException : SystemException
    {
        public SecureMemoryException(string message)
            : base(message)
        {
        }
    }
}
