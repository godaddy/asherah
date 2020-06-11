namespace GoDaddy.Asherah.AppEncryption
{
    public class DefaultPartition : Partition
    {
        public DefaultPartition(string partitionId, string serviceId, string productId)
            : base(partitionId, serviceId, productId)
        {
        }

        public override string ToString()
        {
            return GetType().Name + "[partitionId=" + PartitionId +
                   ", serviceId=" + ServiceId + ", productId=" + ProductId + "]";
        }
    }
}
