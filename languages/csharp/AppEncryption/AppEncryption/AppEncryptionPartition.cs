namespace GoDaddy.Asherah.AppEncryption
{
    public class AppEncryptionPartition
    {
        public AppEncryptionPartition(string partitionId, string systemId, string productId)
        {
            PartitionId = partitionId;
            SystemId = systemId;
            ProductId = productId;
        }

        public string SystemKeyId => "_SK_" + SystemId + "_" + ProductId;

        public string IntermediateKeyId => "_IK_" + PartitionId + "_" + SystemId + "_" + ProductId;

        internal string PartitionId { get; }

        internal string SystemId { get; }

        internal string ProductId { get; }

        public override string ToString()
        {
            return GetType().Name + "[partitionId=" + PartitionId +
                   ", systemId=" + SystemId + ", productId=" + ProductId + "]";
        }
    }
}
