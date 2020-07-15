using GoDaddy.Asherah.AppEncryption.Exceptions;
using GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Regression
{
    [Collection("Configuration collection")]
    public class CrossPartitionDecryptTest
    {
        private readonly ConfigFixture configFixture;

        public CrossPartitionDecryptTest(ConfigFixture configFixture)
        {
            this.configFixture = configFixture;
        }

        [Fact]
        private void TestCrossPartitionDecryptShouldFail()
        {
            byte[] payload = PayloadGenerator.CreateDefaultRandomBytePayload();
            byte[] dataRowRecordBytes;

            string originalPartitionId = "shopper123";
            string alternatePartitionId = "shopper1234";

            using (SessionFactory sessionFactory =
                SessionFactoryGenerator.CreateDefaultSessionFactory(
                    configFixture.KeyManagementService,
                    configFixture.Metastore))
            {
                using (Session<byte[], byte[]> sessionBytes = sessionFactory.GetSessionBytes(originalPartitionId))
                {
                    dataRowRecordBytes = sessionBytes.Encrypt(payload);
                }

                using (Session<byte[], byte[]> sessionBytes = sessionFactory.GetSessionBytes(alternatePartitionId))
                {
                    Assert.Throws<MetadataMissingException>(() => sessionBytes.Decrypt(dataRowRecordBytes));
                }
            }
        }
    }
}
