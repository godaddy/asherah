using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption
{
    [Collection("Logger Fixture collection")]
    public class AppEncryptionPartitionTest
    {
        private const string TestPartitionId = "test_partition_id";
        private const string TestSystemId = "test_system_id";
        private const string TestProductId = "test_product_id";

        private readonly AppEncryptionPartition appEncryptionPartition;

        public AppEncryptionPartitionTest()
        {
            appEncryptionPartition =
                new AppEncryptionPartition(TestPartitionId, TestSystemId, TestProductId);
        }

        [Fact]
        private void TestPartitionCreation()
        {
            Assert.NotNull(appEncryptionPartition);
        }

        [Fact]
        private void TestGetPartitionId()
        {
            string actualTestPartitionId = appEncryptionPartition.PartitionId;
            Assert.Equal(TestPartitionId, actualTestPartitionId);
        }

        [Fact]
        private void TestGetSystemId()
        {
            string actualSystemId = appEncryptionPartition.SystemId;
            Assert.Equal(TestSystemId, actualSystemId);
        }

        [Fact]
        private void TestGetProductId()
        {
            string actualProductId = appEncryptionPartition.ProductId;
            Assert.Equal(TestProductId, actualProductId);
        }

        [Fact]
        private void TestGetSystemKeyId()
        {
            const string systemKeyIdString = "_SK_" + TestSystemId + "_" + TestProductId;
            Assert.Equal(systemKeyIdString, appEncryptionPartition.SystemKeyId);
        }

        [Fact]
        private void TestGetIntermediateKeyId()
        {
            const string intermediateKeyIdString = "_IK_" + TestPartitionId + "_" + TestSystemId + "_" + TestProductId;
            Assert.Equal(intermediateKeyIdString, appEncryptionPartition.IntermediateKeyId);
        }

        [Fact]
        private void TestToString()
        {
            string expectedToStringString = appEncryptionPartition.GetType().Name + "[partitionId=" + TestPartitionId +
                    ", systemId=" + TestSystemId + ", productId=" + TestProductId + "]";
            Assert.Equal(expectedToStringString, appEncryptionPartition.ToString());
        }
    }
}
