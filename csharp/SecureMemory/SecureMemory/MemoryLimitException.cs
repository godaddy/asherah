using System;

namespace GoDaddy.Asherah.SecureMemory
{
    public class MemoryLimitException : SystemException
    {
        public MemoryLimitException(string message)
            : base(message)
        {
        }
    }
}
