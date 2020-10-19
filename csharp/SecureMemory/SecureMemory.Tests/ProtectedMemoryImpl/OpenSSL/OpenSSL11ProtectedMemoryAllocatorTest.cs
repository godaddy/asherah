using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.OpenSSL;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl.OpenSSL
{
    [Collection("Logger Fixture collection")]
    public class OpenSSL11ProtectedMemoryAllocatorTest : IDisposable
    {
        private readonly OpenSSL11ProtectedMemoryAllocatorLP64 openSSL11ProtectedMemoryAllocatorLP64;
        private readonly IConfiguration configuration;
        private readonly SystemInterface systemInterface;

        public OpenSSL11ProtectedMemoryAllocatorTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"heapSize", "4096"},
                {"minimumAllocationSize", "64"},
            }).Build();

            systemInterface = SystemInterface.ConfigureSystemInterface(configuration);

            Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest ctor");
            openSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(configuration, systemInterface);
        }

        public void Dispose()
        {
            openSSL11ProtectedMemoryAllocatorLP64.Dispose();
            Debug.WriteLine("LinuxOpenSSL11ProtectedMemoryAllocatorTest Dispose\n");
        }

        [SkippableFact]
        private void TestAllocFree()
        {
            byte[] origValue = { 1, 2, 3, 4 };
            ulong length = (ulong)origValue.Length;

            IntPtr pointer = openSSL11ProtectedMemoryAllocatorLP64.Alloc(length);

            try
            {
                Marshal.Copy(origValue, 0, pointer, (int)length);

                byte[] retValue = new byte[length];
                Marshal.Copy(pointer, retValue, 0, (int)length);
                Assert.Equal(origValue, retValue);
            }
            finally
            {
                openSSL11ProtectedMemoryAllocatorLP64.Free(pointer, length);
            }
        }

        [SkippableFact]
        private void TestSetNoAccessAfterDispose()
        {
            Debug.WriteLine("TestSetNoAccessAfterDispose");

            var tempOpenSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(configuration, systemInterface);

            tempOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

            Assert.Throws<Exception>(() =>
            {
                tempOpenSSL11ProtectedMemoryAllocatorLP64.SetNoAccess(new IntPtr(-1), 0);
            });
        }

        [SkippableFact]
        private void TestReadAccessAfterDispose()
        {
            Debug.WriteLine("TestReadAccessAfterDispose");

            var tmpOpenSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(configuration, systemInterface);

            tmpOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

            Assert.Throws<Exception>(() =>
            {
                tmpOpenSSL11ProtectedMemoryAllocatorLP64.SetReadAccess(new IntPtr(-1), 0);
            });
        }

        [SkippableFact]
        private void TestReadWriteAccessAfterDispose()
        {
            Debug.WriteLine("TestReadWriteAccessAfterDispose");
            var tmpOpenSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(configuration, systemInterface);

            tmpOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

            Assert.Throws<Exception>(() =>
            {
                tmpOpenSSL11ProtectedMemoryAllocatorLP64.SetReadWriteAccess(new IntPtr(-1), 0);
            });
        }

        [SkippableFact]
        private void TestNullConfiguration()
        {
            Debug.WriteLine("TestNullConfiguration");

            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = new OpenSSL11ProtectedMemoryAllocatorLP64(null, systemInterface);
            });
        }

        [SkippableFact]
        private void TestNullSystemInterface()
        {
            Debug.WriteLine("TestNullSystemInterface");
            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = new OpenSSL11ProtectedMemoryAllocatorLP64(configuration, null);
            });
        }

        [SkippableFact]
        private void TestAllocAfterDispose()
        {
            Debug.WriteLine("TestAllocAfterDispose");

            var tmpOpenSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(configuration, systemInterface);

            tmpOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

            Assert.Throws<Exception>(() =>
            {
                tmpOpenSSL11ProtectedMemoryAllocatorLP64.Alloc(10);
            });
        }

        [SkippableFact]
        private void TestFreeAfterDispose()
        {
            Debug.WriteLine("TestFreeAfterDispose");
            var tmpOpenSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(configuration, systemInterface);

            tmpOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

            Assert.Throws<Exception>(() =>
            {
                tmpOpenSSL11ProtectedMemoryAllocatorLP64.Free(new IntPtr(-1), 0);
            });
        }
    }
}
