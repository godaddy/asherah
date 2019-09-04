using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption
{
    [Collection("Logger Fixture collection")]
    public class PartitionTest
    {
        private const string TestPartitionId = "test_partition_id";
        private const string TestServiceId = "test_service_id";
        private const string TestProductId = "test_product_id";

        private readonly Partition partition;

        public PartitionTest()
        {
            partition = new Partition(TestPartitionId, TestServiceId, TestProductId);
        }

        [Fact]
        private void TestPartitionCreation()
        {
            Assert.NotNull(partition);
        }

        [Fact]
        private void TestGetPartitionId()
        {
            string actualTestPartitionId = partition.PartitionId;
            Assert.Equal(TestPartitionId, actualTestPartitionId);
        }

        [Fact]
        private void TestGetServiceId()
        {
            string actualServiceId = partition.ServiceId;
            Assert.Equal(TestServiceId, actualServiceId);
        }

        [Fact]
        private void TestGetProductId()
        {
            string actualProductId = partition.ProductId;
            Assert.Equal(TestProductId, actualProductId);
        }

        [Fact]
        private void TestGetSystemKeyId()
        {
            const string systemKeyIdString = "_SK_" + TestServiceId + "_" + TestProductId;
            Assert.Equal(systemKeyIdString, partition.SystemKeyId);
        }

        [Fact]
        private void TestGetIntermediateKeyId()
        {
            const string intermediateKeyIdString = "_IK_" + TestPartitionId + "_" + TestServiceId + "_" + TestProductId;
            Assert.Equal(intermediateKeyIdString, partition.IntermediateKeyId);
        }

        [Fact]
        private void TestToString()
        {
            string expectedToStringString = partition.GetType().Name + "[partitionId=" + TestPartitionId +
                    ", serviceId=" + TestServiceId + ", productId=" + TestProductId + "]";
            Assert.Equal(expectedToStringString, partition.ToString());
        }
    }
}
