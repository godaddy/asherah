using System;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc
{
    public class LibcOperationFailedException : SystemException
    {
        public LibcOperationFailedException(string methodName, long result)
            : this(methodName, result, null as object)
        {
        }

        public LibcOperationFailedException(string methodName, long result, Exception exceptionInProgress)
            : this(methodName, result, exceptionInProgress as object)
        {
        }

        public LibcOperationFailedException(string methodName, long result, int errno)
            : this(methodName, result, errno as object)
        {
        }

        protected LibcOperationFailedException(string methodName, long result, object optionalSuffix)
            : base($"Libc call {methodName} failed with result {result} {optionalSuffix}")
        {
        }
    }
}
