using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Libc;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class ProtectedMemorySecretTest : IDisposable
    {
        private readonly Mock<IProtectedMemoryAllocator> protectedMemoryAllocatorMock =
            new Mock<IProtectedMemoryAllocator>();

        private readonly IConfiguration configuration;
        private readonly SystemInterface systemInterface;

        public ProtectedMemorySecretTest()
        {
            TraceListenerConfig.ConfigureTraceListener();

            var configDictionary = new Dictionary<string,string>();
            configDictionary["debugSecrets"] = "true";
            configDictionary["requireSecretDisposal"] = "true";
            configDictionary["heapSize"] = "32000";
            configDictionary["minimumAllocationSize"] = "128";
            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configDictionary)
                .Build();

            systemInterface = SystemInterface.ConfigureSystemInterface(configuration);

            Debug.WriteLine("\nProtectedMemorySecretTest ctor");
        }

        public void Dispose()
        {
            Debug.WriteLine("ProtectedMemorySecretTest End\n");
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestNullConfiguration(IProtectedMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestNullConfiguration");
            using var _ = new ProtectedMemorySecret(
                new byte[] {0, 1},
                protectedMemoryAllocator,
                systemInterface,
                null);
        }

        [Fact]
        private void TestConstructorWithAllocatorReturnsNullShouldFail()
        {
            Debug.WriteLine("TestConstructorWithAllocatorReturnsNullShouldFail");
            protectedMemoryAllocatorMock.Setup(x => x.Alloc(It.IsAny<ulong>())).Returns(IntPtr.Zero);
            Assert.Throws<ProtectedMemoryAllocationFailedException>(() =>
            {
                using var _ = new ProtectedMemorySecret(
                    new byte[] {0, 1},
                    protectedMemoryAllocatorMock.Object,
                    systemInterface,
                    configuration);
            });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretBytesAction(IProtectedMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretBytesAction");
            byte[] secretBytes = { 0, 1 };
            using (ProtectedMemorySecret secret =
                new ProtectedMemorySecret(
                    (byte[])secretBytes.Clone(),
                    protectedMemoryAllocator,
                    systemInterface,
                    configuration))
            {
                secret.WithSecretBytes(decryptedBytes =>
                {
                    Assert.Equal(secretBytes, decryptedBytes);
                });
            }
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretBytesSuccess(IProtectedMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretBytesSuccess");
            byte[] secretBytes = { 0, 1 };
            using (ProtectedMemorySecret secret =
                new ProtectedMemorySecret(
                    (byte[])secretBytes.Clone(),
                    protectedMemoryAllocator,
                    systemInterface,
                    configuration))
            {
                secret.WithSecretBytes(decryptedBytes =>
                {
                    Assert.Equal(secretBytes, decryptedBytes);
                    return true;
                });
            }
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretBytesWithClosedSecretShouldFail(IProtectedMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretBytesWithClosedSecretShouldFail");
            byte[] secretBytes = { 0, 1 };
            ProtectedMemorySecret secret =
                new ProtectedMemorySecret(
                    (byte[])secretBytes.Clone(),
                    protectedMemoryAllocator,
                    systemInterface,
                    configuration);
            secret.Close();
            Assert.Throws<InvalidOperationException>(() => { secret.WithSecretBytes(decryptedBytes => true); });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretUtf8CharsAction(IProtectedMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretUtf8CharsAction");
            char[] secretChars = { 'a', 'b' };
            using (ProtectedMemorySecret secret =
                ProtectedMemorySecret.FromCharArray(
                    (char[])secretChars.Clone(),
                    protectedMemoryAllocator,
                    systemInterface,
                    configuration))
            {
                secret.WithSecretUtf8Chars(decryptedChars =>
                {
                    Assert.Equal(secretChars, decryptedChars);
                });
            }
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretUtf8CharsSuccess(IProtectedMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretUtf8CharsSuccess");
            char[] secretChars = { 'a', 'b' };
            using (ProtectedMemorySecret secret =
                ProtectedMemorySecret.FromCharArray(
                    (char[])secretChars.Clone(),
                    protectedMemoryAllocator,
                    systemInterface,
                    configuration))
            {
                secret.WithSecretUtf8Chars(decryptedChars =>
                {
                    Assert.Equal(secretChars, decryptedChars);
                    return true;
                });
            }
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretIntPtrSuccess(IProtectedMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretIntPtrSuccess");
            char[] secretChars = { 'a', 'b' };
            using (ProtectedMemorySecret secret =
                ProtectedMemorySecret.FromCharArray(
                    secretChars,
                    protectedMemoryAllocator,
                    systemInterface,
                    configuration))
            {
                secret.WithSecretIntPtr((ptr, len) =>
                {
                    Assert.NotEqual(ptr, IntPtr.Zero);
                    Assert.True(len == 2);
                    return true;
                });
            }
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretIntPtrDisposed(IProtectedMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretIntPtrDisposed");
            char[] secretChars = { 'a', 'b' };
            ProtectedMemorySecret secret =
                ProtectedMemorySecret.FromCharArray(
                    secretChars,
                    protectedMemoryAllocator,
                    systemInterface,
                    configuration);

            secret.Dispose();
            Assert.Throws<InvalidOperationException>(() =>
            {
                secret.WithSecretIntPtr((ptr, len) =>
                {
                    return true;
                });
            });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretIntPtrActionSuccess(IProtectedMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretIntPtrActionSuccess");
            char[] secretChars = { 'a', 'b' };
            using (ProtectedMemorySecret secret =
                ProtectedMemorySecret.FromCharArray(
                    secretChars,
                    protectedMemoryAllocator,
                    systemInterface,
                    configuration))
            {
                secret.WithSecretIntPtr((ptr, len) =>
                {
                    Assert.NotEqual(ptr, IntPtr.Zero);
                    Assert.True(len == 2);
                });
            }
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretUtf8CharsWithClosedSecretShouldFail(IProtectedMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretUtf8CharsWithClosedSecretShouldFail");
            char[] secretChars = { 'a', 'b' };
            ProtectedMemorySecret secret =
                ProtectedMemorySecret.FromCharArray(
                    secretChars,
                    protectedMemoryAllocator,
                    systemInterface,
                    configuration);
            secret.Close();
            Assert.Throws<InvalidOperationException>(() => { secret.WithSecretUtf8Chars(decryptedChars => true); });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestCopySecret(IProtectedMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestCopySecret");
            byte[] secretBytes = { 0, 1 };
            using (ProtectedMemorySecret secret =
                new ProtectedMemorySecret(
                    (byte[])secretBytes.Clone(),
                    protectedMemoryAllocator,
                    systemInterface,
                    configuration))
            {
                using (ProtectedMemorySecret secretCopy = (ProtectedMemorySecret)secret.CopySecret())
                {
                    secretCopy.WithSecretBytes(decryptedBytes =>
                    {
                        Assert.Equal(secretBytes, decryptedBytes);
                    });
                }
            }
        }

        [Fact]
        private void TestCloseWithClosedSecretShouldNoop()
        {
            Debug.WriteLine("TestCloseWithClosedSecretShouldNoop");
            byte[] secretBytes = { 0, 1 };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Mock<WindowsProtectedMemoryAllocatorLLP64> protectedMemoryAllocatorWindowsMock =
                    new Mock<WindowsProtectedMemoryAllocatorLLP64>(systemInterface) {CallBase = true};

                ProtectedMemorySecret secret =
                    new ProtectedMemorySecret(
                        secretBytes,
                        protectedMemoryAllocatorWindowsMock.Object,
                        systemInterface,
                        configuration);
                secret.Close();
                secret.Close();
                protectedMemoryAllocatorWindowsMock.Verify(
                    x => x.Free(It.IsAny<IntPtr>(), It.IsAny<ulong>()), Times.Exactly(1));
            }
            else
            {
                Mock<LibcProtectedMemoryAllocatorLP64> protectedMemoryAllocatorLibcMock =
                    new Mock<LibcProtectedMemoryAllocatorLP64>(systemInterface) {CallBase = true};

                ProtectedMemorySecret secret =
                    new ProtectedMemorySecret(
                        secretBytes,
                        protectedMemoryAllocatorLibcMock.Object,
                        systemInterface,
                        configuration);
                secret.Close();
                secret.Close();
                protectedMemoryAllocatorLibcMock.Verify(
                    x => x.Free(It.IsAny<IntPtr>(), It.IsAny<ulong>()), Times.Exactly(1));
            }
        }

        [Fact]
        private void TestAllocatorSetNoAccessFailure()
        {
            Debug.WriteLine("TestAllocatorSetNoAccessFailure");
            byte[] secretBytes = { 0, 1 };
            IProtectedMemoryAllocator allocator;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Mock<WindowsProtectedMemoryAllocatorLLP64> protectedMemoryAllocatorWindowsMock =
                    new Mock<WindowsProtectedMemoryAllocatorLLP64>(systemInterface) { CallBase = true };

                protectedMemoryAllocatorWindowsMock.Setup(x => x.SetNoAccess(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new Exception());

                allocator = protectedMemoryAllocatorWindowsMock.Object;
            }
            else
            {
                Mock<LibcProtectedMemoryAllocatorLP64> protectedMemoryAllocatorLibcMock =
                    new Mock<LibcProtectedMemoryAllocatorLP64>(systemInterface) { CallBase = true };

                protectedMemoryAllocatorLibcMock.Setup(x => x.SetNoAccess(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new Exception());

                allocator = protectedMemoryAllocatorLibcMock.Object;
            }

            Assert.Throws<Exception>(() =>
            {
                using ProtectedMemorySecret secret =
                    new ProtectedMemorySecret(
                        secretBytes,
                        allocator,
                        systemInterface,
                        configuration);
            });
        }

        [Fact]
        private void TestAllocatorIntPtrSetNoAccessFailure()
        {
            Debug.WriteLine("TestAllocatorSetNoAccessFailure");
            byte[] secretBytes = { 0, 1 };
            IProtectedMemoryAllocator allocator;

            var handle = GCHandle.Alloc(secretBytes, GCHandleType.Pinned);
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Mock<WindowsProtectedMemoryAllocatorLLP64> protectedMemoryAllocatorWindowsMock =
                        new Mock<WindowsProtectedMemoryAllocatorLLP64>(systemInterface) { CallBase = true };

                    protectedMemoryAllocatorWindowsMock.Setup(x => x.SetNoAccess(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                        .Throws(new Exception());

                    allocator = protectedMemoryAllocatorWindowsMock.Object;
                }
                else
                {
                    Mock<LibcProtectedMemoryAllocatorLP64> protectedMemoryAllocatorLibcMock =
                        new Mock<LibcProtectedMemoryAllocatorLP64>(systemInterface) { CallBase = true };

                    protectedMemoryAllocatorLibcMock.Setup(x => x.SetNoAccess(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                        .Throws(new Exception());

                    allocator = protectedMemoryAllocatorLibcMock.Object;
                }

                Assert.Throws<Exception>(() =>
                {
                    using ProtectedMemorySecret secret =
                        new ProtectedMemorySecret(
                            handle.AddrOfPinnedObject(),
                            (ulong)secretBytes.LongLength,
                            allocator,
                            systemInterface,
                            configuration);
                });
            }
            finally
            {
                handle.Free();
            }
        }

        [Fact]
        private void TestAllocatorSetNoDumpFailure()
        {
            Debug.WriteLine("TestAllocatorSetNoDumpFailure");
            byte[] secretBytes = { 0, 1 };

            SystemInterface mockedSystemInterface;
            IProtectedMemoryAllocator allocator;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var mockSystemInterfaceWindows = new Mock<WindowsSystemInterfaceImpl> { CallBase = true };
                mockSystemInterfaceWindows.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new Exception("IGNORE_INTENTIONAL_ERROR"));
                mockedSystemInterface = mockSystemInterfaceWindows.Object;
                allocator = new WindowsProtectedMemoryAllocatorLLP64(configuration, mockedSystemInterface, new WindowsMemoryEncryption());
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var mockSystemInterfaceLinux = new Mock<LinuxSystemInterfaceImpl> { CallBase = true };
                mockSystemInterfaceLinux.Setup(x =>
                        x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()
                        ))
                    .Throws(new Exception("IGNORE_INTENTIONAL_ERROR"));
                mockedSystemInterface = mockSystemInterfaceLinux.Object;
                allocator = new LibcProtectedMemoryAllocatorLP64(mockedSystemInterface);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var mockSystemInterfaceMacOS = new Mock<MacOSSystemInterfaceImpl> { CallBase = true };
                mockSystemInterfaceMacOS.Setup(x =>
                        x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()
                        ))
                    .Throws(new Exception("IGNORE_INTENTIONAL_ERROR"));
                mockedSystemInterface = mockSystemInterfaceMacOS.Object;
                allocator = new LibcProtectedMemoryAllocatorLP64(mockedSystemInterface);
            }
            else
            {
                throw new Exception("Unknown platform: " + RuntimeInformation.OSDescription);
            }

            Assert.Throws<Exception>(() =>
            {
                using ProtectedMemorySecret secret =
                    new ProtectedMemorySecret(
                        secretBytes,
                        allocator,
                        mockedSystemInterface,
                        configuration);
            });
        }

        // Borderline integration test, but still runs fast and can help catch critical regression
        [Fact]
        private void TestWithSecretBytesMultiThreadedAccess()
        {
            Debug.WriteLine("TestWithSecretBytesMultiThreadedAccess start");
            using ISecretFactory secretFactory = new ProtectedMemorySecretFactory(configuration);
            byte[] secretBytes = { 0, 1, 2, 3 };
            Debug.WriteLine("Creating secret");
            using Secret secret = secretFactory.CreateSecret(secretBytes.Clone() as byte[]);
            // Submit large number of tasks to a reasonably sized thread pool to verify concurrency
            // semantics around the protected memory management
            const int numThreads = 100;

            // Get the current settings and try to force minWorkers
            ThreadPool.GetMinThreads(out _, out var currentMinCompletionPortThreads);
            Assert.True(ThreadPool.SetMinThreads(numThreads, currentMinCompletionPortThreads));

            const int numTasks = numThreads * 1000;
            long completedTasks = 0;
            CountdownEvent countdown = new CountdownEvent(numTasks);

            Parallel.ForEach(Enumerable.Range(0, numTasks), i =>
            {
                ThreadPool.QueueUserWorkItem(
                    state =>
                    {
                        // ReSharper disable once AccessToDisposedClosure
                        secret.WithSecretBytes(decryptedBytes =>
                        {
                            Assert.Equal(secretBytes, decryptedBytes);
                        });
                        Interlocked.Increment(ref completedTasks);
                        countdown.Signal();
                    }, null);
            });

            // Wait for all threads to complete
            Debug.WriteLine("Waiting for threads");
            countdown.Wait();
            Debug.WriteLine("Threads finished");
            Assert.Equal(numTasks, completedTasks);

            Debug.WriteLine("TestWithSecretBytesMultiThreadedAccess end");
        }
    }
}
