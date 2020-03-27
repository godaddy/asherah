using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests.ProtectedMemoryImpl
{
    [Collection("Logger Fixture collection")]
    public class ProtectedMemorySecretTest
    {
        private readonly Mock<IProtectedMemoryAllocator> protectedMemoryAllocatorMock =
            new Mock<IProtectedMemoryAllocator>();

        private readonly IProtectedMemoryAllocator protectedMemoryAllocatorController;

        public ProtectedMemorySecretTest()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                protectedMemoryAllocatorController = new MacOSProtectedMemoryAllocatorLP64();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                protectedMemoryAllocatorController = new LinuxProtectedMemoryAllocatorLP64();
            }
        }

        [Fact]
        private void TestConstructorWithAllocatorReturnsNullShouldFail()
        {
            protectedMemoryAllocatorMock.Setup(x => x.Alloc(It.IsAny<ulong>())).Returns(IntPtr.Zero);
            Assert.Throws<ProtectedMemoryAllocationFailedException>(() =>
            {
                new ProtectedMemorySecret(new byte[] { 0, 1 }, protectedMemoryAllocatorMock.Object);
            });
        }

        [Fact]
        private void TestWithSecretBytesAction()
        {
            byte[] secretBytes = { 0, 1 };
            using (ProtectedMemorySecret secret =
                new ProtectedMemorySecret((byte[])secretBytes.Clone(), protectedMemoryAllocatorController))
            {
                secret.WithSecretBytes(decryptedBytes =>
                {
                    Assert.Equal(secretBytes, decryptedBytes);
                });
            }
        }

        [Fact]
        private void TestWithSecretBytesSuccess()
        {
            byte[] secretBytes = { 0, 1 };
            using (ProtectedMemorySecret secret =
                new ProtectedMemorySecret((byte[])secretBytes.Clone(), protectedMemoryAllocatorController))
            {
                secret.WithSecretBytes(decryptedBytes =>
                {
                    Assert.Equal(secretBytes, decryptedBytes);
                    return true;
                });
            }
        }

        [Fact]
        private void TestWithSecretBytesWithClosedSecretShouldFail()
        {
            byte[] secretBytes = { 0, 1 };
            ProtectedMemorySecret secret =
                new ProtectedMemorySecret((byte[])secretBytes.Clone(), protectedMemoryAllocatorController);
            secret.Close();
            Assert.Throws<InvalidOperationException>(() => { secret.WithSecretBytes(decryptedBytes => true); });
        }

        [Fact]
        private void TestWithSecretUtf8CharsAction()
        {
            char[] secretChars = { 'a', 'b' };
            using (ProtectedMemorySecret secret =
                ProtectedMemorySecret.FromCharArray(secretChars, protectedMemoryAllocatorController))
            {
                secret.WithSecretUtf8Chars(decryptedChars =>
                {
                    Assert.Equal(secretChars, decryptedChars);
                });
            }
        }

        [Fact]
        private void TestWithSecretUtf8CharsSuccess()
        {
            char[] secretChars = { 'a', 'b' };
            using (ProtectedMemorySecret secret =
                ProtectedMemorySecret.FromCharArray(secretChars, protectedMemoryAllocatorController))
            {
                secret.WithSecretUtf8Chars(decryptedChars =>
                {
                    Assert.Equal(secretChars, decryptedChars);
                    return true;
                });
            }
        }

        [Fact]
        private void TestWithSecretUtf8CharsWithClosedSecretShouldFail()
        {
            char[] secretChars = { 'a', 'b' };
            ProtectedMemorySecret secret =
                ProtectedMemorySecret.FromCharArray(secretChars, protectedMemoryAllocatorController);
            secret.Close();
            Assert.Throws<InvalidOperationException>(() => { secret.WithSecretUtf8Chars(decryptedChars => true); });
        }

        [Fact]
        private void TestCopySecret()
        {
            byte[] secretBytes = { 0, 1 };
            using (ProtectedMemorySecret secret =
                new ProtectedMemorySecret((byte[])secretBytes.Clone(), protectedMemoryAllocatorController))
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
            byte[] secretBytes = { 0, 1 };

            // TODO : Need to determine if we can stub out the protectedMemoryAllocatorMock.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Mock<MacOSProtectedMemoryAllocatorLP64> protectedMemoryAllocatorMacOSMock =
                    new Mock<MacOSProtectedMemoryAllocatorLP64> { CallBase = true };

                ProtectedMemorySecret secret =
                    new ProtectedMemorySecret(secretBytes, protectedMemoryAllocatorMacOSMock.Object);
                secret.Close();
                secret.Close();
                protectedMemoryAllocatorMacOSMock.Verify(
                    x => x.Free(It.IsAny<IntPtr>(), It.IsAny<ulong>()), Times.Exactly(1));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Mock<LinuxProtectedMemoryAllocatorLP64> protectedMemoryAllocatorLinuxMock =
                    new Mock<LinuxProtectedMemoryAllocatorLP64> { CallBase = true };

                ProtectedMemorySecret secret =
                    new ProtectedMemorySecret(secretBytes, protectedMemoryAllocatorLinuxMock.Object);
                secret.Close();
                secret.Close();
                protectedMemoryAllocatorLinuxMock.Verify(
                    x => x.Free(It.IsAny<IntPtr>(), It.IsAny<ulong>()), Times.Exactly(1));
            }
        }

        // Borderline integration test, but still runs fast and can help catch critical regression
        [Fact]
        private void TestWithSecretBytesMultiThreadedAccess()
        {
            ISecretFactory secretFactory = new ProtectedMemorySecretFactory();
            byte[] secretBytes = { 0, 1, 2, 3 };
            Secret secret = secretFactory.CreateSecret(secretBytes.Clone() as byte[]);

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
            countdown.Wait();
            Assert.Equal(numTasks, completedTasks);
        }
    }
}
