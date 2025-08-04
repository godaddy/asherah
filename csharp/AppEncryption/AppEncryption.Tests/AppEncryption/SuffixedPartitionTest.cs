using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption
{
    public class SuffixedPartitionTest
    {
        private const string TestPartitionId = "test_partition_id";
        private const string TestServiceId = "test_service_id";
        private const string TestProductId = "test_product_id";
        private const string TestSuffixRegion = "test_suffix_region";

        private readonly SuffixedPartition partition;

        public SuffixedPartitionTest()
        {
            partition = new SuffixedPartition(TestPartitionId, TestServiceId, TestProductId, TestSuffixRegion);
        }

        [Fact]
        private void TestSuffixedPartitionCreation()
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
            const string systemKeyIdString = "_SK_" + TestServiceId + "_" + TestProductId + "_" + TestSuffixRegion;
            Assert.Equal(systemKeyIdString, partition.SystemKeyId);
        }

        [Fact]
        private void TestGetIntermediateKeyId()
        {
            const string intermediateKeyIdString = "_IK_" + TestPartitionId + "_" + TestServiceId + "_" + TestProductId + "_" + TestSuffixRegion;
            Assert.Equal(intermediateKeyIdString, partition.IntermediateKeyId);
        }

        [Fact]
        private void TestToString()
        {
            string expectedToStringString = partition.GetType().Name + "[partitionId=" + TestPartitionId +
                    ", serviceId=" + TestServiceId + ", productId=" + TestProductId + ", regionSuffix=" + TestSuffixRegion + "]";
            Assert.Equal(expectedToStringString, partition.ToString());
        }

        [Fact]
        private void TestIsValidIntermediaKeyId()
        {
            const string intermediateKeyIdStringSuffixed = "_IK_" + TestPartitionId + "_" + TestServiceId + "_" + TestProductId + "_" + TestSuffixRegion;
            Assert.True(partition.IsValidIntermediateKeyId(intermediateKeyIdStringSuffixed));

            const string intermediateKeyIdStringSuffixedOther = "_IK_" + TestPartitionId + "_" + TestServiceId + "_" + TestProductId + "_" + "other_suffix";
            Assert.True(partition.IsValidIntermediateKeyId(intermediateKeyIdStringSuffixedOther));

            const string intermediateKeyIdString = "_IK_" + TestPartitionId + "_" + TestServiceId + "_" + TestProductId;
            Assert.True(partition.IsValidIntermediateKeyId(intermediateKeyIdString));

            const string invalidId = "_IK_some_other_partition" + "_" + TestServiceId + "_" + TestProductId;
            Assert.False(partition.IsValidIntermediateKeyId(invalidId));
        }
    }
}
