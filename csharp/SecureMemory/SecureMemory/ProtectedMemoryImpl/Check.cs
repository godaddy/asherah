using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl
{
    public static class Check
    {
        public static readonly IntPtr InvalidPointer = new IntPtr(-1);

        public static void IntPtr(IntPtr intPointer, string methodName)
        {
            if (intPointer == System.IntPtr.Zero || intPointer == InvalidPointer)
            {
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
                Debug.WriteLine($"Check.Zero failed for {methodName} result: {result} errno: {errno}");
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
                Debug.WriteLine($"Check.Result failed for {methodName} result: {result} expected: {expected} errno: {errno}");
                throw new LibcOperationFailedException(methodName, result, errno);
            }
        }

        public static void Zero(int result, string methodName, Exception exceptionInProgress)
        {
            if (result != 0)
            {
                var errno = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Check.Zero failed for {methodName} result: {result} errno: {errno}");
                throw new LibcOperationFailedException(methodName, result, exceptionInProgress);
            }
        }
    }
}
