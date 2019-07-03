using System;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl
{
    public class MemoryLimitException : SystemException
    {
        public MemoryLimitException(string message)
            : base(message)
        {
        }
    }
}
