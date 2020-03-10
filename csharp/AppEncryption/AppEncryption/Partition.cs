namespace GoDaddy.Asherah.AppEncryption
{
    public class Partition
    {
        public Partition(string partitionId, string serviceId, string productId)
        {
            PartitionId = partitionId;
            ServiceId = serviceId;
            ProductId = productId;
        }

        public string SystemKeyId => "_SK_" + ServiceId + "_" + ProductId;

        public string IntermediateKeyId => "_IK_" + PartitionId + "_" + ServiceId + "_" + ProductId;

        internal string PartitionId { get; }

        internal string ServiceId { get; }

        internal string ProductId { get; }

        public override string ToString()
        {
            return GetType().Name + "[partitionId=" + PartitionId +
                   ", serviceId=" + ServiceId + ", productId=" + ProductId + "]";
        }
    }
}
