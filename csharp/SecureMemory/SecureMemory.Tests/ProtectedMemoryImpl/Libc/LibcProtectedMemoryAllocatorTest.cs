using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.Libc
{
    /// <summary>
    /// These tests are a bit impure and expect the platform to be based on libc implementations
    /// Pure tests have been promoted to ProtectedMemoryAllocatorTest.cs
    /// </summary>
    [Collection("Logger Fixture collection")]
    public class LibcProtectedMemoryAllocatorTest : IDisposable
    {
        private readonly SystemInterface systemInterface;
        private readonly LibcProtectedMemoryAllocatorLP64 libcProtectedMemoryAllocator;

        public LibcProtectedMemoryAllocatorTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"heapSize", "32000"},
                {"minimumAllocationSize", "128"},
#if DEBUG
                {"openSSLPath", @"C:\Program Files\OpenSSL\bin"},
#endif
            }).Build();

            systemInterface = SystemInterface.ConfigureSystemInterface(configuration);
            libcProtectedMemoryAllocator = new LibcProtectedMemoryAllocatorLP64(systemInterface);

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest ctor");
        }

        public void Dispose()
        {
            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.Dispose");
            libcProtectedMemoryAllocator?.Dispose();
        }

        [SkippableFact]
        private void TestDisableCoreDumpGlobally()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestDisableCoreDumpGlobally");

            Assert.False(systemInterface.AreCoreDumpsGloballyDisabled());
            systemInterface.DisableCoreDumpGlobally();
            Assert.True(systemInterface.AreCoreDumpsGloballyDisabled());
        }

        [SkippableFact]
        private void TestAllocWithSetNoDumpErrorShouldFail()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestAllocWithSetNoDumpErrorShouldFail");

            IProtectedMemoryAllocator allocator;
            Mock<LinuxSystemInterfaceImpl> mockSystemInterfaceLinux;
            Mock<MacOSSystemInterfaceImpl> mockSystemInterfaceMacOS;
            SystemInterface mockedSystemInterface;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                mockSystemInterfaceMacOS = new Mock<MacOSSystemInterfaceImpl> { CallBase = true };
                mockSystemInterfaceMacOS.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new LibcOperationFailedException("IGNORE_INTENTIONAL_ERROR", 1));
                mockedSystemInterface = mockSystemInterfaceMacOS.Object;
                allocator = new LibcProtectedMemoryAllocatorLP64(mockedSystemInterface);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                mockSystemInterfaceLinux = new Mock<LinuxSystemInterfaceImpl> { CallBase = true };
                mockSystemInterfaceLinux.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new LibcOperationFailedException("IGNORE_INTENTIONAL_ERROR", 1));
                mockedSystemInterface = mockSystemInterfaceLinux.Object;
                allocator = new LibcProtectedMemoryAllocatorLP64(mockedSystemInterface);
            }
            else
            {
                return;
            }

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                allocator.Alloc(1);
            });
        }

        [SkippableFact]
        private void TestCheckPointerWithRegularPointerShouldSucceed()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithRegularPointerShouldSucceed");

            IntPtr pointer = libcProtectedMemoryAllocator.Alloc(1);
            try
            {
                Check.IntPtr(pointer, "TestCheckPointerWithRegularPointerShouldSucceed");
            }
            finally
            {
                libcProtectedMemoryAllocator.Free(pointer, 1);
            }
        }

        [SkippableFact]
        private void TestFreeWithInvalidLengthShouldFail()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestFreeWithInvalidLengthShouldFail");

            IntPtr fakePtr = IntPtr.Add(IntPtr.Zero, 1);
            Assert.Throws<LibcOperationFailedException>(() => libcProtectedMemoryAllocator.Free(fakePtr, 0));
        }

        [SkippableFact]
        private void TestHugeAllocShouldFail()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestHugeAllocShouldFail");

            Assert.Throws<MemoryLimitException>(() => libcProtectedMemoryAllocator.Alloc((ulong)Int32.MaxValue + 1));
        }

        [SkippableFact]
        private void TestCheckPointerWithNullPointerShouldFail()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithNullPointerShouldFail");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.IntPtr(IntPtr.Zero, "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [SkippableFact]
        private void TestCheckPointerWithMapFailedPointerShouldFail()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckPointerWithMapFailedPointerShouldFail");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.IntPtr(new IntPtr(-1), "IGNORE_INTENTIONAL_ERROR");
            });
        }

        [SkippableFact]
        private void TestCheckZeroWithZeroResult()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroWithZeroResult");

            Check.Zero(0, "TestCheckZeroWithZeroResult");
        }

        [SkippableFact]
        private void TestCheckZeroWithNonZeroResult()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroWithNonZeroResult");

            Assert.Throws<LibcOperationFailedException>(() => { Check.Zero(1, "IGNORE_INTENTIONAL_ERROR"); });
        }

        [SkippableFact]
        private void TestCheckZeroThrowableWithZeroResult()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroThrowableWithZeroResult");

            Check.Zero(0, "TestCheckZeroThrowableWithZeroResult", new InvalidOperationException());
        }

        [SkippableFact]
        private void TestCheckZeroThrowableWithNonZeroResult()
        {
            Skip.If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

            Debug.WriteLine("LibcProtectedMemoryAllocatorTest.TestCheckZeroThrowableWithNonZeroResult");

            Assert.Throws<LibcOperationFailedException>(() =>
            {
                Check.Zero(1, "IGNORE_INTENTIONAL_ERROR", new InvalidOperationException());
            });
        }

        [SkippableFact]
        private void TestAllocFree()
        {
            Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Linux));

            byte[] origValue = { 1, 2, 3, 4 };
            ulong length = (ulong)origValue.Length;

            IntPtr pointer = libcProtectedMemoryAllocator.Alloc(length);

            try
            {
                Marshal.Copy(origValue, 0, pointer, (int)length);

                byte[] retValue = new byte[length];
                Marshal.Copy(pointer, retValue, 0, (int)length);
                Assert.Equal(origValue, retValue);
            }
            finally
            {
                libcProtectedMemoryAllocator.Free(pointer, length);
            }
        }
    }
}
