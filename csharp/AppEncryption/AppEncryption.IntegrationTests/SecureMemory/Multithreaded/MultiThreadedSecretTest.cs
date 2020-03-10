using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.Logging;
using GoDaddy.Asherah.SecureMemory;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl;
using Microsoft.Extensions.Logging;
using Xunit;

using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.SecureMemory.Multithreaded
{
    public class MultiThreadedSecretTest
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<MultiThreadedSecretTest>();
        private readonly byte[] payload;

        public MultiThreadedSecretTest()
        {
            payload = PayloadGenerator.CreateRandomBytePayload(PayloadSizeBytes);
        }

        [Fact]
        private void MultiThreadedWithSecretBytesAccess()
        {
            ISecretFactory secretFactory = new ProtectedMemorySecretFactory();
            Secret secret = secretFactory.CreateSecret(payload.Clone() as byte[]);

            // Get the current settings and try to force minWorkers
            ThreadPool.GetMinThreads(out _, out var currentMinIOC);
            Assert.True(ThreadPool.SetMinThreads(NumThreads, currentMinIOC));

            int completedTasks = 0;

            Parallel.ForEach(Enumerable.Range(0, NumThreads), i =>
            {
                try
                {
                    secret.WithSecretBytes(decryptedBytes =>
                    {
                        Assert.Equal(payload, decryptedBytes);
                        Interlocked.Increment(ref completedTasks);
                    });
                }
                catch (ThreadInterruptedException e)
                {
                    Logger.LogError(e, "Unexpected error during call");
                    throw;
                }
            });
            Assert.Equal(NumThreads, completedTasks);
        }
    }
}
