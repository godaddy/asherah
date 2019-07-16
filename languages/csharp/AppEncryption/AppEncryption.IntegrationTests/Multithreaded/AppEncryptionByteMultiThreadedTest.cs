using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.AppEncryption.KeyManagement;
using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;
using Xunit;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Multithreaded
{
    [Collection("Configuration collection")]
    public class AppEncryptionByteMultiThreadedTest : IDisposable
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<AppEncryptionByteMultiThreadedTest>();

        private readonly byte[] payload;
        private readonly AppEncryptionSessionFactory appEncryptionSessionFactory;
        private readonly string partitionId;
        private readonly AppEncryption<byte[], byte[]> appEncryptionBytes;

        public AppEncryptionByteMultiThreadedTest(ConfigFixture configFixture)
        {
            payload = PayloadGenerator.CreateDefaultRandomBytePayload();
            appEncryptionSessionFactory = SessionFactoryGenerator.CreateDefaultAppEncryptionSessionFactory(configFixture.KeyManagementService, configFixture.MetastorePersistence);
            partitionId = DefaultPartitionId + "_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset();
            appEncryptionBytes = appEncryptionSessionFactory.GetAppEncryptionBytes(partitionId);
        }

        public void Dispose()
        {
            appEncryptionBytes.Dispose();
            appEncryptionSessionFactory.Dispose();
        }

        [Fact]
        public void AppEncryptionEncryptMultipleThreads()
        {
            Logger.LogInformation("Running AppEncryptionEncryptMultipleThreads test with {numThreads} threads", NumThreads);

            // Get the current settings and try to force minWorkers
            ThreadPool.GetMinThreads(out _, out var currentMinIOC);
            Assert.True(ThreadPool.SetMinThreads(NumThreads, currentMinIOC));

            long completedTasks = 0;

            Parallel.ForEach(Enumerable.Range(0, NumThreads), i =>
            {
                try
                {
                    byte[] drr = appEncryptionBytes.Encrypt(payload);

                    Assert.Equal(payload, appEncryptionBytes.Decrypt(drr));
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
