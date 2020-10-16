using System;

namespace GoDaddy.Asherah.PlatformNative.LP64.Libc
{
    public class LibcOperationFailedException : SystemException
    {
        public LibcOperationFailedException(string methodName, long result)
            : this(methodName, result, null as object)
        {
        }

        public LibcOperationFailedException(string methodName, long result, Exception exceptionInProgress)
            : base($"Libc call {methodName} failed with result {result}", exceptionInProgress)
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
