namespace GoDaddy.Asherah.AppEncryption
{
    /// <summary>
    /// An additional layer of abstraction which generates the system key and intermediate key ids.
    /// It uses a <see cref="PartitionId"/> to uniquely identify a <see cref="Session{TP,TD}"/>, i.e. every partition id
    /// should have its own session.
    /// A payload encrypted using some partition id, cannot be decrypted using a different one.
    /// </summary>
    public class Partition
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Partition"/> class using the provided parameters.
        /// </summary>
        ///
        /// <param name="partitionId">A unique identifier for a <see cref="Session{TP,TD}"/>.</param>
        /// <param name="serviceId">A unique identifier for a service, used to create a <see cref="SessionFactory"/>
        /// object.</param>
        /// <param name="productId">A unique identifier for a product, used to create a <see cref="SessionFactory"/>
        /// object.</param>
        public Partition(string partitionId, string serviceId, string productId)
        {
            PartitionId = partitionId;
            ServiceId = serviceId;
            ProductId = productId;
        }

        /// <summary>
        /// Gets get the system key id.
        /// </summary>
        public virtual string SystemKeyId => "_SK_" + ServiceId + "_" + ProductId;

        /// <summary>
        /// Gets get the intermediate key id.
        /// </summary>
        public virtual string IntermediateKeyId => "_IK_" + PartitionId + "_" + ServiceId + "_" + ProductId;

        internal string PartitionId { get; }

        internal string ServiceId { get; }

        internal string ProductId { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return GetType().Name + "[partitionId=" + PartitionId +
                   ", serviceId=" + ServiceId + ", productId=" + ProductId + "]";
        }
    }
}
