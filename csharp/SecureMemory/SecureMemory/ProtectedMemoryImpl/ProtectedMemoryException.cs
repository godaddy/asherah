using System;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl
{
    public class ProtectedMemoryException : SystemException
    {
        public ProtectedMemoryException(string message)
            : base(message)
        {
        }
    }
}
