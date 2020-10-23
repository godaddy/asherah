using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.OpenSSL;
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
        private readonly IOpenSSLCrypto crypto;
        public OpenSSL11ProtectedMemoryAllocatorTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>()
            {
                {"heapSize", "4096"},
                {"minimumAllocationSize", "64"},
#if DEBUG
                {"openSSLPath", @"C:\Program Files\OpenSSL\bin"},
#endif
            }).Build();

            systemInterface = SystemInterface.ConfigureSystemInterface(configuration);
            crypto = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? (IOpenSSLCrypto) new OpenSSLCryptoWindows(configuration)
                : new OpenSSLCryptoLibc(configuration);
            Debug.WriteLine("\nLinuxOpenSSL11ProtectedMemoryAllocatorTest ctor");
            try
            {
                openSSL11ProtectedMemoryAllocatorLP64 =
                    new OpenSSL11ProtectedMemoryAllocatorLP64(
                        configuration,
                        systemInterface,
                        new OpenSSLCryptProtectMemory("aes-256-gcm",
                            systemInterface,
                            crypto),
                        crypto);
            }
            catch (OpenSSLSecureHeapUnavailableException)
            {
                openSSL11ProtectedMemoryAllocatorLP64 = null;
            }
        }

        public void Dispose()
        {
            openSSL11ProtectedMemoryAllocatorLP64?.Dispose();
            Debug.WriteLine("LinuxOpenSSL11ProtectedMemoryAllocatorTest Dispose\n");
        }

        [SkippableFact]
        private void TestAllocFree()
        {
            Skip.If(openSSL11ProtectedMemoryAllocatorLP64 == null);

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
            Skip.If(openSSL11ProtectedMemoryAllocatorLP64 == null);

            Debug.WriteLine("TestSetNoAccessAfterDispose");

            var tempOpenSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(
                configuration,
                systemInterface,
                new OpenSSLCryptProtectMemory(
                    "aes-256-gcm",
                    systemInterface,
                    crypto),
                crypto);


            tempOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

            Assert.Throws<Exception>(() =>
            {
                tempOpenSSL11ProtectedMemoryAllocatorLP64.SetNoAccess(new IntPtr(-1), 0);
            });
        }

        [SkippableFact]
        private void TestReadAccessAfterDispose()
        {
            Skip.If(openSSL11ProtectedMemoryAllocatorLP64 == null);

            Debug.WriteLine("TestReadAccessAfterDispose");

            var tmpOpenSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(
                configuration,
                systemInterface,
                new OpenSSLCryptProtectMemory(
                    "aes-256-gcm",
                    systemInterface,
                    crypto),
                crypto);

            tmpOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

            Assert.Throws<Exception>(() =>
            {
                tmpOpenSSL11ProtectedMemoryAllocatorLP64.SetReadAccess(new IntPtr(-1), 0);
            });
        }

        [SkippableFact]
        private void TestReadWriteAccessAfterDispose()
        {
            Skip.If(openSSL11ProtectedMemoryAllocatorLP64 == null);

            Debug.WriteLine("TestReadWriteAccessAfterDispose");
            var tmpOpenSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(
                configuration,
                systemInterface,
                new OpenSSLCryptProtectMemory(
                    "aes-256-gcm",
                    systemInterface,
                    crypto),
                crypto);

            tmpOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

            Assert.Throws<Exception>(() =>
            {
                tmpOpenSSL11ProtectedMemoryAllocatorLP64.SetReadWriteAccess(new IntPtr(-1), 0);
            });
        }

        [SkippableFact]
        private void TestNullConfiguration()
        {
            Skip.If(openSSL11ProtectedMemoryAllocatorLP64 == null);

            Debug.WriteLine("TestNullConfiguration");

            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = new OpenSSL11ProtectedMemoryAllocatorLP64(
                    null,
                    systemInterface,
                    null,
                    null
                    );
            });
        }

        [SkippableFact]
        private void TestNullSystemInterface()
        {
            Skip.If(openSSL11ProtectedMemoryAllocatorLP64 == null);

            Debug.WriteLine("TestNullSystemInterface");
            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = new OpenSSL11ProtectedMemoryAllocatorLP64(
                    configuration,
                    null,
                    new OpenSSLCryptProtectMemory("aes-256-gcm", systemInterface, crypto), crypto);
            });
        }

        [SkippableFact]
        private void TestAllocAfterDispose()
        {
            Skip.If(openSSL11ProtectedMemoryAllocatorLP64 == null);

            Debug.WriteLine("TestAllocAfterDispose");

            var tmpOpenSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(
                configuration,
                systemInterface,
                new OpenSSLCryptProtectMemory("aes-256-gcm", systemInterface, crypto), crypto);

            tmpOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

            Assert.Throws<Exception>(() =>
            {
                tmpOpenSSL11ProtectedMemoryAllocatorLP64.Alloc(10);
            });
        }

        [SkippableFact]
        private void TestFreeAfterDispose()
        {
            Skip.If(openSSL11ProtectedMemoryAllocatorLP64 == null);

            Debug.WriteLine("TestFreeAfterDispose");
            var tmpOpenSSL11ProtectedMemoryAllocatorLP64 = new OpenSSL11ProtectedMemoryAllocatorLP64(
                configuration,
                systemInterface,
                new OpenSSLCryptProtectMemory("aes-256-gcm", systemInterface, crypto),
                null);

            tmpOpenSSL11ProtectedMemoryAllocatorLP64.Dispose();

            Assert.Throws<Exception>(() =>
            {
                tmpOpenSSL11ProtectedMemoryAllocatorLP64.Free(new IntPtr(-1), 0);
            });
        }
    }
}
