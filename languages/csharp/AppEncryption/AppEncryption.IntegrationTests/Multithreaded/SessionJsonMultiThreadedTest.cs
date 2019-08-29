using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using static GoDaddy.Asherah.AppEncryption.IntegrationTests.TestHelpers.Constants;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Multithreaded
{
    [Collection("Configuration collection")]
    public class SessionJsonMultiThreadedTest : IDisposable
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<SessionJsonMultiThreadedTest>();

        private readonly JObject payload;
        private readonly SessionFactory sessionFactory;
        private readonly string partitionId;
        private readonly Session<JObject, byte[]> sessionJson;

        public SessionJsonMultiThreadedTest(ConfigFixture configFixture)
        {
            payload = PayloadGenerator.CreateDefaultRandomJsonPayload();
            sessionFactory = SessionFactoryGenerator.CreateDefaultSessionFactory(
                configFixture.KeyManagementService,
                configFixture.Metastore);
            partitionId = DefaultPartitionId + "_" + DateTimeUtils.GetCurrentTimeAsUtcIsoDateTimeOffset();
            sessionJson = sessionFactory.GetSessionJson(partitionId);
        }

        public void Dispose()
        {
            sessionJson.Dispose();
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
                    byte[] drr = sessionJson.Encrypt(payload);

                    Assert.Equal(payload, sessionJson.Decrypt(drr));
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
