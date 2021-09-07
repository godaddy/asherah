using System;

namespace GoDaddy.Asherah.SecureMemory.SecureMemoryImpl
{
    public class MemoryLimitException : SystemException
    {
        public MemoryLimitException(string message)
            : base(message)
        {
        }
    }
}
