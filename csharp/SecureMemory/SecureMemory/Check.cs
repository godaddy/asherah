using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.Libc;

namespace GoDaddy.Asherah.SecureMemory
{
    public static class Check
    {
        public static readonly IntPtr InvalidPointer = new IntPtr(-1);

        public static void IntPointer(IntPtr intPointer, string methodName)
        {
            if (intPointer == IntPtr.Zero || intPointer == InvalidPointer)
            {
                var errno = Marshal.GetLastWin32Error();
                Debug.WriteLine($"****************** Check.ValidatePointer failed for {methodName} result: {intPointer} errno: {errno}");
                throw new LibcOperationFailedException(methodName, intPointer.ToInt64());
            }
        }

        public static void Zero(int result, string methodName)
        {
            if (result != 0)
            {
                // NOTE: Even though this references Win32 it actually returns
                // the last errno on non-Windows platforms.
                var errno = Marshal.GetLastWin32Error();
                Debug.WriteLine($"****************** Check.Zero failed for {methodName} result: {result} errno: {errno}");
                throw new LibcOperationFailedException(methodName, result, errno);
            }
        }

        public static void Result(int result, int expected, string methodName)
        {
            if (result != expected)
            {
                // NOTE: Even though this references Win32 it actually returns
                // the last errno on non-Windows platforms.
                var errno = Marshal.GetLastWin32Error();
                Debug.WriteLine($"****************** Check.Result failed for {methodName} result: {result} expected: {expected} errno: {errno}");
                throw new LibcOperationFailedException(methodName, result, errno);
            }
        }

        public static void Zero(int result, string methodName, Exception exceptionInProgress)
        {
            if (result != 0)
            {
                var errno = Marshal.GetLastWin32Error();
                Debug.WriteLine($"****************** Check.Zero failed for {methodName} result: {result} errno: {errno}");
                throw new LibcOperationFailedException(methodName, result, exceptionInProgress);
            }
        }
    }
}
