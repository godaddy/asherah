using System;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;

namespace GoDaddy.Asherah.PlatformNative.LLP64.Windows
{
    internal class WindowsOperationFailedException : LibcOperationFailedException
    {
        public WindowsOperationFailedException(string methodName, long result)
            : this(methodName, result, null as object)
        {
        }

        public WindowsOperationFailedException(string methodName, long result, Exception exceptionInProgress)
            : this(methodName, result, exceptionInProgress as object)
        {
        }

        public WindowsOperationFailedException(string methodName, long result, int errno)
            : this(methodName, result, errno as object)
        {
        }

        protected WindowsOperationFailedException(string methodName, long result, object optionalSuffix)
            : base(methodName, result, optionalSuffix)
        {
        }
    }
}
