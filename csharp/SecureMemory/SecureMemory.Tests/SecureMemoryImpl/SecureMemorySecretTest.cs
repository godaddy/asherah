using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.MacOS;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class SecureMemorySecretTest : IDisposable
    {
        private readonly Mock<ISecureMemoryAllocator> secureMemoryAllocatorMock =
            new Mock<ISecureMemoryAllocator>();

        private readonly IConfiguration configuration;

        public SecureMemorySecretTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            var configDictionary = new Dictionary<string,string>();
            configDictionary["debugSecrets"] = "true";
            configDictionary["requireSecretDisposal"] = "true";
            configDictionary["heapSize"] = "32000";
            configDictionary["minimumAllocationSize"] = "128";
            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configDictionary)
                .Build();

            Debug.WriteLine("\nSecureMemorySecretTest ctor");
        }

        public void Dispose()
        {
            Debug.WriteLine("SecureMemorySecretTest End\n");
        }

        [Theory]
        [ClassData(typeof(SecureMemoryImpl.AllocatorGenerator))]
        private void TestNullConfiguration(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestNullConfiguration");
            using (var secret = new SecureMemorySecret(new byte[] { 0, 1 }, protectedMemoryAllocator, null))
            {
            }
        }

        [Fact]
        private void TestConstructorWithAllocatorReturnsNullShouldFail()
        {
            Debug.WriteLine("TestConstructorWithAllocatorReturnsNullShouldFail");
            secureMemoryAllocatorMock.Setup(x => x.Alloc(It.IsAny<ulong>())).Returns(IntPtr.Zero);
            Assert.Throws<SecureMemoryAllocationFailedException>(() =>
            {
                using (var secret = new SecureMemorySecret(new byte[] { 0, 1 }, secureMemoryAllocatorMock.Object, configuration))
                {
                }
            });
        }

        [Theory]
        [ClassData(typeof(SecureMemoryImpl.AllocatorGenerator))]
        private void TestWithSecretBytesAction(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretBytesAction");
            byte[] secretBytes = { 0, 1 };
            using (SecureMemorySecret secret =
                new SecureMemorySecret((byte[])secretBytes.Clone(), protectedMemoryAllocator, configuration))
            {
                secret.WithSecretBytes(decryptedBytes =>
                {
                    Assert.Equal(secretBytes, decryptedBytes);
                });
            }
        }

        [Theory]
        [ClassData(typeof(SecureMemoryImpl.AllocatorGenerator))]
        private void TestWithSecretBytesSuccess(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretBytesSuccess");
            byte[] secretBytes = { 0, 1 };
            using (SecureMemorySecret secret =
                new SecureMemorySecret((byte[])secretBytes.Clone(), protectedMemoryAllocator, configuration))
            {
                secret.WithSecretBytes(decryptedBytes =>
                {
                    Assert.Equal(secretBytes, decryptedBytes);
                    return true;
                });
            }
        }

        [Theory]
        [ClassData(typeof(SecureMemoryImpl.AllocatorGenerator))]
        private void TestWithSecretBytesWithClosedSecretShouldFail(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretBytesWithClosedSecretShouldFail");
            byte[] secretBytes = { 0, 1 };
            SecureMemorySecret secret =
                new SecureMemorySecret((byte[])secretBytes.Clone(), protectedMemoryAllocator, configuration);
            secret.Close();
            Assert.Throws<InvalidOperationException>(() => { secret.WithSecretBytes(decryptedBytes => true); });
        }

        [Theory]
        [ClassData(typeof(SecureMemoryImpl.AllocatorGenerator))]
        private void TestWithSecretUtf8CharsAction(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretUtf8CharsAction");
            char[] secretChars = { 'a', 'b' };
            using (SecureMemorySecret secret =
                SecureMemorySecret.FromCharArray(secretChars, protectedMemoryAllocator, configuration))
            {
                secret.WithSecretUtf8Chars(decryptedChars =>
                {
                    Assert.Equal(secretChars, decryptedChars);
                });
            }
        }

        [Theory]
        [ClassData(typeof(SecureMemoryImpl.AllocatorGenerator))]
        private void TestWithSecretUtf8CharsSuccess(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretUtf8CharsSuccess");
            char[] secretChars = { 'a', 'b' };
            using (SecureMemorySecret secret =
                SecureMemorySecret.FromCharArray(secretChars, protectedMemoryAllocator, configuration))
            {
                secret.WithSecretUtf8Chars(decryptedChars =>
                {
                    Assert.Equal(secretChars, decryptedChars);
                    return true;
                });
            }
        }

        [Theory]
        [ClassData(typeof(SecureMemoryImpl.AllocatorGenerator))]
        private void TestWithSecretIntPtrSuccess(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretIntPtrSuccess");
            char[] secretChars = { 'a', 'b' };
            using (SecureMemorySecret secret =
                SecureMemorySecret.FromCharArray(secretChars, protectedMemoryAllocator, configuration))
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
        [ClassData(typeof(SecureMemoryImpl.AllocatorGenerator))]
        private void TestWithSecretIntPtrDisposed(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretIntPtrDisposed");
            char[] secretChars = { 'a', 'b' };
            SecureMemorySecret secret =
                SecureMemorySecret.FromCharArray(secretChars, protectedMemoryAllocator, configuration);

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
        [ClassData(typeof(SecureMemoryImpl.AllocatorGenerator))]
        private void TestWithSecretIntPtrActionSuccess(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretIntPtrActionSuccess");
            char[] secretChars = { 'a', 'b' };
            using (SecureMemorySecret secret =
                SecureMemorySecret.FromCharArray(secretChars, protectedMemoryAllocator, configuration))
            {
                secret.WithSecretIntPtr((ptr, len) =>
                {
                    Assert.NotEqual(ptr, IntPtr.Zero);
                    Assert.True(len == 2);
                });
            }
        }

        [Theory]
        [ClassData(typeof(SecureMemoryImpl.AllocatorGenerator))]
        private void TestWithSecretUtf8CharsWithClosedSecretShouldFail(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretUtf8CharsWithClosedSecretShouldFail");
            char[] secretChars = { 'a', 'b' };
            SecureMemorySecret secret =
                SecureMemorySecret.FromCharArray(secretChars, protectedMemoryAllocator, configuration);
            secret.Close();
            Assert.Throws<InvalidOperationException>(() => { secret.WithSecretUtf8Chars(decryptedChars => true); });
        }

        [Theory]
        [ClassData(typeof(SecureMemoryImpl.AllocatorGenerator))]
        private void TestCopySecret(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestCopySecret");
            byte[] secretBytes = { 0, 1 };
            using (SecureMemorySecret secret =
                new SecureMemorySecret((byte[])secretBytes.Clone(), protectedMemoryAllocator, configuration))
            {
                using (SecureMemorySecret secretCopy = (SecureMemorySecret)secret.CopySecret())
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

            // TODO : Need to determine if we can stub out the protectedMemoryAllocatorMock.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Mock<MacOSSecureMemoryAllocatorLP64> protectedMemoryAllocatorMacOSMock =
                    new Mock<MacOSSecureMemoryAllocatorLP64> { CallBase = true };

                SecureMemorySecret secret =
                    new SecureMemorySecret(secretBytes, protectedMemoryAllocatorMacOSMock.Object, configuration);
                secret.Close();
                secret.Close();
                protectedMemoryAllocatorMacOSMock.Verify(
                    x => x.Free(It.IsAny<IntPtr>(), It.IsAny<ulong>()), Times.Exactly(1));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Mock<LinuxSecureMemoryAllocatorLP64> protectedMemoryAllocatorLinuxMock =
                    new Mock<LinuxSecureMemoryAllocatorLP64> { CallBase = true };

                SecureMemorySecret secret =
                    new SecureMemorySecret(secretBytes, protectedMemoryAllocatorLinuxMock.Object, configuration);
                secret.Close();
                secret.Close();
                protectedMemoryAllocatorLinuxMock.Verify(
                    x => x.Free(It.IsAny<IntPtr>(), It.IsAny<ulong>()), Times.Exactly(1));
            }
        }

        [Fact]
        private void TestAllocatorSetNoAccessFailure()
        {
            Debug.WriteLine("TestAllocatorSetNoAccessFailure");
            byte[] secretBytes = { 0, 1 };
            ISecureMemoryAllocator allocator = null;

            // TODO : Need to determine if we can stub out the protectedMemoryAllocatorMock.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Mock<MacOSSecureMemoryAllocatorLP64> protectedMemoryAllocatorMacOSMock =
                    new Mock<MacOSSecureMemoryAllocatorLP64> { CallBase = true };

                protectedMemoryAllocatorMacOSMock.Setup(x => x.SetNoAccess(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new Exception());

                allocator = protectedMemoryAllocatorMacOSMock.Object;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Mock<LinuxSecureMemoryAllocatorLP64> protectedMemoryAllocatorLinuxMock =
                    new Mock<LinuxSecureMemoryAllocatorLP64> { CallBase = true };

                protectedMemoryAllocatorLinuxMock.Setup(x => x.SetNoAccess(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new Exception());

                allocator = protectedMemoryAllocatorLinuxMock.Object;
            }
            else
            {
                return;
            }

            Assert.Throws<Exception>(() =>
            {
                using SecureMemorySecret secret =
                    new SecureMemorySecret(secretBytes, allocator, configuration);
            });
        }

        [Fact]
        private void TestAllocatorSetNoDumpFailure()
        {
            Debug.WriteLine("TestAllocatorSetNoDumpFailure");
            byte[] secretBytes = { 0, 1 };
            ISecureMemoryAllocator allocator = null;

            // TODO : Need to determine if we can stub out the protectedMemoryAllocatorMock.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Mock<MacOSSecureMemoryAllocatorLP64> secureMemoryAllocatorMacOSMock =
                    new Mock<MacOSSecureMemoryAllocatorLP64> { CallBase = true };

                secureMemoryAllocatorMacOSMock.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new Exception());

                allocator = secureMemoryAllocatorMacOSMock.Object;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Mock<LinuxSecureMemoryAllocatorLP64> protectedMemoryAllocatorLinuxMock =
                    new Mock<LinuxSecureMemoryAllocatorLP64>(configuration) { CallBase = true };

                protectedMemoryAllocatorLinuxMock.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new Exception());

                allocator = secureMemoryAllocatorMock.Object;
            }
            else
            {
                return;
            }

            Assert.Throws<SecureMemoryAllocationFailedException>(() =>
            {
                using SecureMemorySecret secret =
                    new SecureMemorySecret(secretBytes, allocator, configuration);
            });
        }

        // Borderline integration test, but still runs fast and can help catch critical regression
        [Fact]
        private void TestWithSecretBytesMultiThreadedAccess()
        {
            Debug.WriteLine("TestWithSecretBytesMultiThreadedAccess start");
            using (ISecretFactory secretFactory = new SecureMemorySecretFactory(configuration))
            {
                byte[] secretBytes = { 0, 1, 2, 3 };
                Debug.WriteLine("Creating secret");
                using (Secret secret = secretFactory.CreateSecret(secretBytes.Clone() as byte[]))
                {
                    // Submit large number of tasks to a reasonably sized thread pool to verify concurrency
                    // semantics around the protected memory management
                    const int numThreads = 100;

                    // Get the current settings and try to force minWorkers
                    ThreadPool.GetMinThreads(out _, out var currentMinIOC);
                    Assert.True(ThreadPool.SetMinThreads(numThreads, currentMinIOC));

                    const int numTasks = numThreads * 1000;
                    long completedTasks = 0;
                    CountdownEvent countdown = new CountdownEvent(numTasks);

                    Parallel.ForEach(Enumerable.Range(0, numTasks), i =>
                    {
                        ThreadPool.QueueUserWorkItem(
                            state =>
                            {
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
                }
            }

            Debug.WriteLine("TestWithSecretBytesMultiThreadedAccess end");
        }
    }
}
