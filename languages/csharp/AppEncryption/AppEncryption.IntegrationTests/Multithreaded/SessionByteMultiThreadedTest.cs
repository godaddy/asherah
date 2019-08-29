using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;
using Xunit;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Multithreaded
{
    [Collection("Configuration collection")]
    public class SessionByteMultiThreadedTest : IDisposable
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<SessionByteMultiThreadedTest>();

        private readonly byte[] payload;
        private readonly SessionFactory sessionFactory;
        private readonly string partitionId;
        private readonly Session<byte[], byte[]> sessionBytes;

        public SessionByteMultiThreadedTest(ConfigFixture configFixture)
        {
            payload = PayloadGenerator.CreateDefaultRandomBytePayload();
            sessionFactory = SessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.KeyManagementService,
                configFixture.Metastore);
            partitionId = DefaultPartitionId + "_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset();
            sessionBytes = sessionFactory.GetSessionBytes(partitionId);
        }

        public void Dispose()
        {
            sessionBytes.Dispose();
            sessionFactory.Dispose();
        }

        [Fact]
        public void SessionEncryptMultipleThreads()
        {
            Logger.LogInformation("Running SessionEncryptMultipleThreads test with {numThreads} threads", NumThreads);

            // Get the current settings and try to force minWorkers
            ThreadPool.GetMinThreads(out _, out var currentMinIOC);
            Assert.True(ThreadPool.SetMinThreads(NumThreads, currentMinIOC));

            long completedTasks = 0;

            Parallel.ForEach(Enumerable.Range(0, NumThreads), i =>
            {
                try
                {
                    byte[] drr = sessionBytes.Encrypt(payload);

                    Assert.Equal(payload, sessionBytes.Decrypt(drr));
                    Interlocked.Increment(ref completedTasks);
                }
                catch (ThreadInterruptedException e)
                {
                    Logger.LogError(e, "Unexpected error during call: ");
                    throw;
                }
            });

            // Wait for all threads to complete
            Assert.Equal(NumThreads, completedTasks);
        }
    }
}
