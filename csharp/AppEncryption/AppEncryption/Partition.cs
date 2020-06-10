namespace GoDaddy.Asherah.AppEncryption
{
    public abstract class Partition
    {
        protected Partition(string partitionId, string serviceId, string productId)
        {
            PartitionId = partitionId;
            ServiceId = serviceId;
            ProductId = productId;
        }

        public virtual string SystemKeyId => "_SK_" + ServiceId + "_" + ProductId;

        public virtual string IntermediateKeyId => "_IK_" + PartitionId + "_" + ServiceId + "_" + ProductId;

        internal string PartitionId { get; }

        internal string ServiceId { get; }

        internal string ProductId { get; }
    }
}
