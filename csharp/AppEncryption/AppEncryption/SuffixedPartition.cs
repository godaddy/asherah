namespace GoDaddy.Asherah.AppEncryption
{
    public class SuffixedPartition : Partition
    {
        private readonly string regionSuffix;

        /// <summary>
        /// Initializes a new instance of the <see cref="SuffixedPartition"/> class. An implementation of
        /// <see cref="Partition"/> that is used to support Global Tables in
        /// <see cref="GoDaddy.Asherah.AppEncryption.Persistence.DynamoDbMetastoreImpl"/>.
        /// </summary>
        ///
        /// <param name="partitionId">A unique identifier for a <see cref="Session{TP,TD}"/>.</param>
        /// <param name="serviceId">A unique identifier for a service, used to create a <see cref="SessionFactory"/>
        /// object.</param>
        /// <param name="productId">A unique identifier for a product, used to create a <see cref="SessionFactory"/>
        /// object.</param>
        /// <param name="regionSuffix">The suffix to be added to a lookup key when using DynamoDB Global Tables.</param>
        public SuffixedPartition(string partitionId, string serviceId, string productId, string regionSuffix)
            : base(partitionId, serviceId, productId)
        {
            this.regionSuffix = regionSuffix;
        }

        /// <inheritdoc/>
        public override string SystemKeyId => base.SystemKeyId + "_" + regionSuffix;

        /// <inheritdoc/>
        public override string IntermediateKeyId => base.IntermediateKeyId + "_" + regionSuffix;

        /// <inheritdoc/>
        public override string ToString()
        {
            return GetType().Name + "[partitionId=" + PartitionId +
                   ", serviceId=" + ServiceId + ", productId=" + ProductId + ", regionSuffix=" + regionSuffix + "]";
        }
    }
}
