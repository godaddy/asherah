using GoDaddy.Asherah.AppEncryption.Core;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Core
{
    public class SessionPartitionTests
    {
        private const string TestPartitionId = "test_partition_id";
        private const string TestServiceId = "test_service_id";
        private const string TestProductId = "test_product_id";
        private const string TestSuffix = "test_suffix";

        [Fact]
        public void Constructor_WithoutSuffix_CreatesPartition()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId);

            Assert.NotNull(partition);
        }

        [Fact]
        public void Constructor_WithSuffix_CreatesPartition()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId, TestSuffix);

            Assert.NotNull(partition);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(TestSuffix)]
        public void PartitionId_ReturnsCorrectValue(string suffix)
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId, suffix);

            Assert.Equal(TestPartitionId, partition.PartitionId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(TestSuffix)]
        public void ServiceId_ReturnsCorrectValue(string suffix)
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId, suffix);

            Assert.Equal(TestServiceId, partition.ServiceId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(TestSuffix)]
        public void ProductId_ReturnsCorrectValue(string suffix)
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId, suffix);

            Assert.Equal(TestProductId, partition.ProductId);
        }

        [Fact]
        public void Suffix_WithoutSuffix_ReturnsNull()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId);

            Assert.Null(partition.Suffix);
        }

        [Fact]
        public void Suffix_WithSuffix_ReturnsCorrectValue()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId, TestSuffix);

            Assert.Equal(TestSuffix, partition.Suffix);
        }

        [Fact]
        public void SystemKeyId_WithoutSuffix_ReturnsCorrectFormat()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId);

            const string expected = "_SK_" + TestServiceId + "_" + TestProductId;
            Assert.Equal(expected, partition.SystemKeyId);
        }

        [Fact]
        public void SystemKeyId_WithSuffix_ReturnsCorrectFormat()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId, TestSuffix);

            const string expected = "_SK_" + TestServiceId + "_" + TestProductId + "_" + TestSuffix;
            Assert.Equal(expected, partition.SystemKeyId);
        }

        [Fact]
        public void IntermediateKeyId_WithoutSuffix_ReturnsCorrectFormat()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId);

            const string expected = "_IK_" + TestPartitionId + "_" + TestServiceId + "_" + TestProductId;
            Assert.Equal(expected, partition.IntermediateKeyId);
        }

        [Fact]
        public void IntermediateKeyId_WithSuffix_ReturnsCorrectFormat()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId, TestSuffix);

            const string expected = "_IK_" + TestPartitionId + "_" + TestServiceId + "_" + TestProductId + "_" + TestSuffix;
            Assert.Equal(expected, partition.IntermediateKeyId);
        }

        [Fact]
        public void IsValidIntermediateKeyId_WithoutSuffix_MatchesExactKeyId()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId);

            const string validKeyId = "_IK_" + TestPartitionId + "_" + TestServiceId + "_" + TestProductId;
            Assert.True(partition.IsValidIntermediateKeyId(validKeyId));
        }

        [Fact]
        public void IsValidIntermediateKeyId_WithoutSuffix_RejectsInvalidKeyId()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId);

            const string invalidKeyId = "_IK_other_partition_" + TestServiceId + "_" + TestProductId;
            Assert.False(partition.IsValidIntermediateKeyId(invalidKeyId));
        }

        [Fact]
        public void IsValidIntermediateKeyId_WithSuffix_MatchesExactSuffixedKeyId()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId, TestSuffix);

            const string suffixedKeyId = "_IK_" + TestPartitionId + "_" + TestServiceId + "_" + TestProductId + "_" + TestSuffix;
            Assert.True(partition.IsValidIntermediateKeyId(suffixedKeyId));
        }

        [Fact]
        public void IsValidIntermediateKeyId_WithSuffix_MatchesBaseKeyId()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId, TestSuffix);

            const string baseKeyId = "_IK_" + TestPartitionId + "_" + TestServiceId + "_" + TestProductId;
            Assert.True(partition.IsValidIntermediateKeyId(baseKeyId));
        }

        [Fact]
        public void IsValidIntermediateKeyId_WithSuffix_MatchesOtherSuffixedKeyId()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId, TestSuffix);

            const string otherSuffixedKeyId = "_IK_" + TestPartitionId + "_" + TestServiceId + "_" + TestProductId + "_other_suffix";
            Assert.True(partition.IsValidIntermediateKeyId(otherSuffixedKeyId));
        }

        [Fact]
        public void IsValidIntermediateKeyId_WithSuffix_RejectsInvalidKeyId()
        {
            var partition = new SessionPartition(TestPartitionId, TestServiceId, TestProductId, TestSuffix);

            const string invalidKeyId = "_IK_other_partition_" + TestServiceId + "_" + TestProductId;
            Assert.False(partition.IsValidIntermediateKeyId(invalidKeyId));
        }
    }
}
