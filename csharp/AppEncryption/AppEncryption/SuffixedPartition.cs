namespace GoDaddy.Asherah.AppEncryption
{
    public class SuffixedPartition : Partition
    {
        private readonly string regionSuffix;

        public SuffixedPartition(string partitionId, string serviceId, string productId, string regionSuffix)
            : base(partitionId, serviceId, productId)
        {
            this.regionSuffix = regionSuffix;
        }

        public override string SystemKeyId => base.SystemKeyId + "_" + regionSuffix;

        public override string IntermediateKeyId => base.IntermediateKeyId + "_" + regionSuffix;

        public override string ToString()
        {
            return GetType().Name + "[partitionId=" + PartitionId +
                   ", serviceId=" + ServiceId + ", productId=" + ProductId + ", regionSuffix=" + regionSuffix + "]";
        }
    }
}
