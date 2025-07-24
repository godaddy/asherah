using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS;
using GoDaddy.Asherah.SecureMemory.Tests.SecureMemoryImpl;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    [Collection("Logger Fixture collection")]
    public class SecureMemorySecretTest : IDisposable
    {
        private static readonly byte[] TestBytes = new byte[] { 0, 1 };
        private readonly IConfiguration configuration;

        public SecureMemorySecretTest()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);

            var configDictionary = new Dictionary<string, string>();
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
            Debug.WriteLine("SecureMemorySecretTest.Dispose");
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestNullConfiguration(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestNullConfiguration");
            using (var secret = new SecureMemorySecret(TestBytes, protectedMemoryAllocator, null))
            {
            }
        }

        [Fact]
        private void TestConstructorWithAllocatorReturnsNullShouldFail()
        {
            Debug.WriteLine("TestConstructorWithAllocatorReturnsNullShouldFail");
            var secureMemoryAllocatorMock = new Mock<ISecureMemoryAllocator>();
            secureMemoryAllocatorMock.Setup(x => x.Alloc(It.IsAny<ulong>())).Returns(IntPtr.Zero);
            Assert.Throws<SecureMemoryAllocationFailedException>(() =>
            {
                using (var secret = new SecureMemorySecret(TestBytes, secureMemoryAllocatorMock.Object, configuration))
                {
                }
            });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretBytesAction(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretBytesAction");
            using var secret =
              new SecureMemorySecret((byte[])TestBytes.Clone(), protectedMemoryAllocator, configuration);
            secret.WithSecretBytes(decryptedBytes =>
            {
                Assert.Equal(TestBytes, decryptedBytes);
            });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretBytesSuccess(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretBytesSuccess");
            using var secret =
              new SecureMemorySecret((byte[])TestBytes.Clone(), protectedMemoryAllocator, configuration);
            secret.WithSecretBytes(decryptedBytes =>
            {
                Assert.Equal(TestBytes, decryptedBytes);
                return true;
            });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretBytesWithClosedSecretShouldFail(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretBytesWithClosedSecretShouldFail");
            var secret =
                new SecureMemorySecret((byte[])TestBytes.Clone(), protectedMemoryAllocator, configuration);
            secret.Close();
            Assert.Throws<InvalidOperationException>(() => { secret.WithSecretBytes(_ => true); });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretUtf8CharsAction(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretUtf8CharsAction");
            char[] secretChars = { 'a', 'b' };
            using var secret =
              SecureMemorySecret.FromCharArray(secretChars, protectedMemoryAllocator, configuration);
            secret.WithSecretUtf8Chars(decryptedChars =>
            {
                Assert.Equal(secretChars, decryptedChars);
            });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretUtf8CharsSuccess(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretUtf8CharsSuccess");
            char[] secretChars = { 'a', 'b' };
            using var secret =
              SecureMemorySecret.FromCharArray(secretChars, protectedMemoryAllocator, configuration);
            secret.WithSecretUtf8Chars(decryptedChars =>
            {
                Assert.Equal(secretChars, decryptedChars);
                return true;
            });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretIntPtrSuccess(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretIntPtrSuccess");
            char[] secretChars = { 'a', 'b' };
            using var secret =
              SecureMemorySecret.FromCharArray(secretChars, protectedMemoryAllocator, configuration);
            secret.WithSecretIntPtr((ptr, len) =>
            {
                Assert.NotEqual(ptr, IntPtr.Zero);
                Assert.True(len == 2);
                return true;
            });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretIntPtrDisposed(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretIntPtrDisposed");
            char[] secretChars = { 'a', 'b' };
            var secret =
                SecureMemorySecret.FromCharArray(secretChars, protectedMemoryAllocator, configuration);

            secret.Dispose();
            Assert.Throws<InvalidOperationException>(() =>
            {
                secret.WithSecretIntPtr((_, _) =>
                {
                    return true;
                });
            });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretIntPtrActionSuccess(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretIntPtrActionSuccess");
            char[] secretChars = { 'a', 'b' };
            using var secret =
              SecureMemorySecret.FromCharArray(secretChars, protectedMemoryAllocator, configuration);
            secret.WithSecretIntPtr((ptr, len) =>
            {
                Assert.NotEqual(ptr, IntPtr.Zero);
                Assert.True(len == 2);
            });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestWithSecretUtf8CharsWithClosedSecretShouldFail(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestWithSecretUtf8CharsWithClosedSecretShouldFail");
            char[] secretChars = { 'a', 'b' };
            var secret =
                SecureMemorySecret.FromCharArray(secretChars, protectedMemoryAllocator, configuration);
            secret.Close();
            Assert.Throws<InvalidOperationException>(() => { secret.WithSecretUtf8Chars(_ => true); });
        }

        [Theory]
        [ClassData(typeof(AllocatorGenerator))]
        private void TestCopySecret(ISecureMemoryAllocator protectedMemoryAllocator)
        {
            Debug.WriteLine("TestCopySecret");
            using var secret =
              new SecureMemorySecret((byte[])TestBytes.Clone(), protectedMemoryAllocator, configuration);
            using var secretCopy = (SecureMemorySecret)secret.CopySecret();
            secretCopy.WithSecretBytes(decryptedBytes =>
            {
                Assert.Equal(TestBytes, decryptedBytes);
            });
        }

        [Fact]
        private void TestCloseWithClosedSecretShouldNoop()
        {
            Debug.WriteLine("TestCloseWithClosedSecretShouldNoop");
            // TODO : Need to determine if we can stub out the protectedMemoryAllocatorMock.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var protectedMemoryAllocatorMacOSMock =
                    new Mock<MacOSProtectedMemoryAllocatorLP64> { CallBase = true };

                var secret =
                    new SecureMemorySecret(TestBytes, protectedMemoryAllocatorMacOSMock.Object, configuration);
                secret.Close();
                secret.Close();
                protectedMemoryAllocatorMacOSMock.Verify(
                    x => x.Free(It.IsAny<IntPtr>(), It.IsAny<ulong>()), Times.Exactly(1));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var protectedMemoryAllocatorLinuxMock =
                    new Mock<LinuxProtectedMemoryAllocatorLP64> { CallBase = true };

                var secret =
                    new SecureMemorySecret(TestBytes, protectedMemoryAllocatorLinuxMock.Object, configuration);
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
            ISecureMemoryAllocator allocator = null;

            // TODO : Need to determine if we can stub out the protectedMemoryAllocatorMock.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var protectedMemoryAllocatorMacOSMock =
                    new Mock<MacOSProtectedMemoryAllocatorLP64> { CallBase = true };

                protectedMemoryAllocatorMacOSMock.Setup(x => x.SetNoAccess(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(new Exception());

                allocator = protectedMemoryAllocatorMacOSMock.Object;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var protectedMemoryAllocatorLinuxMock =
                    new Mock<LinuxProtectedMemoryAllocatorLP64> { CallBase = true };

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
                using var secret =
                    new SecureMemorySecret(TestBytes, allocator, configuration);
            });
        }

        [Fact]
        private void TestAllocatorSetNoDumpFailure()
        {
            Debug.WriteLine("TestAllocatorSetNoDumpFailure");
            byte[] secretBytes = { 0, 1 };
            ISecureMemoryAllocator allocator = null;

            var setNoDumpException = new Exception("SetNoDump failed");

            // TODO : Need to determine if we can stub out the protectedMemoryAllocatorMock.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var secureMemoryAllocatorMacOSMock =
                    new Mock<MacOSProtectedMemoryAllocatorLP64> { CallBase = true };

                secureMemoryAllocatorMacOSMock.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(setNoDumpException);

                allocator = secureMemoryAllocatorMacOSMock.Object;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var protectedMemoryAllocatorLinuxMock =
                    new Mock<LinuxOpenSSL11ProtectedMemoryAllocatorLP64>(configuration) { CallBase = true };

                protectedMemoryAllocatorLinuxMock.Setup(x => x.SetNoDump(It.IsAny<IntPtr>(), It.IsAny<ulong>()))
                    .Throws(setNoDumpException);

                allocator = protectedMemoryAllocatorLinuxMock.Object;
            }
            else
            {
                return;
            }

            var exception = Assert.Throws<SecureMemoryAllocationFailedException>(() =>
            {
                using var secret =
                    new SecureMemorySecret(secretBytes, allocator, configuration);
            });
            Assert.Equal(setNoDumpException, exception.InnerException);
        }

        // Borderline integration test, but still runs fast and can help catch critical regression
        [Fact]
        private void TestWithSecretBytesMultiThreadedAccess()
        {
            Debug.WriteLine("TestWithSecretBytesMultiThreadedAccess start");
            using (var secretFactory = new SecureMemorySecretFactory(configuration))
            {
                byte[] secretBytes = { 0, 1, 2, 3 };
                Debug.WriteLine("Creating secret");
                using (var secret = secretFactory.CreateSecret(secretBytes.Clone() as byte[]))
                {
                    // Submit large number of tasks to a reasonably sized thread pool to verify concurrency
                    // semantics around the protected memory management
                    const int numThreads = 100;

                    // Get the current settings and try to force minWorkers
                    ThreadPool.GetMinThreads(out _, out var currentMinIOC);
                    Assert.True(ThreadPool.SetMinThreads(numThreads, currentMinIOC));

                    const int numTasks = numThreads * 1000;
                    long completedTasks = 0;
                    var countdown = new CountdownEvent(numTasks);

                    Parallel.ForEach(Enumerable.Range(0, numTasks), _ =>
                    {
                        ThreadPool.QueueUserWorkItem(
                            _ =>
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
