using GoDaddy.Asherah.AppEncryption.Core;

namespace GoDaddy.Asherah.AppEncryption
{
    /// <summary>
    /// An additional layer of abstraction which generates the system key and intermediate key ids.
    /// It uses a <see cref="PartitionId"/> to uniquely identify a <see cref="Session{TP,TD}"/>, i.e. every partition id
    /// should have its own session.
    /// A payload encrypted using some partition id, cannot be decrypted using a different one.
    /// </summary>
    public abstract class Partition : ISessionPartition
    {
        private readonly SessionPartition _sessionPartition;

        /// <summary>
        /// Initializes a new instance of the <see cref="Partition"/> class.
        /// </summary>
        /// <param name="partitionId">A unique identifier for this partition.</param>
        /// <param name="serviceId">A unique identifier for a service.</param>
        /// <param name="productId">A unique identifier for a product.</param>
        protected Partition(string partitionId, string serviceId, string productId)
            : this(partitionId, serviceId, productId, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Partition"/> class with an optional suffix.
        /// </summary>
        /// <param name="partitionId">A unique identifier for this partition.</param>
        /// <param name="serviceId">A unique identifier for a service.</param>
        /// <param name="productId">A unique identifier for a product.</param>
        /// <param name="suffix">Optional suffix appended to key ids.</param>
        protected Partition(string partitionId, string serviceId, string productId, string suffix)
        {
            _sessionPartition = new SessionPartition(partitionId, serviceId, productId, suffix);
        }

        /// <summary>
        /// Gets the system key id.
        /// </summary>
        public string SystemKeyId => _sessionPartition.SystemKeyId;

        /// <summary>
        /// Gets the intermediate key id.
        /// </summary>
        public string IntermediateKeyId => _sessionPartition.IntermediateKeyId;

        /// <summary>
        /// Gets the partition id.
        /// </summary>
        public string PartitionId => _sessionPartition.PartitionId;

        /// <summary>
        /// Gets the service id.
        /// </summary>
        public string ServiceId => _sessionPartition.ServiceId;

        /// <summary>
        /// Gets the product id.
        /// </summary>
        public string ProductId => _sessionPartition.ProductId;

        /// <summary>
        /// Gets the optional suffix appended to key ids. Returns null for base partitions.
        /// </summary>
        public string Suffix => _sessionPartition.Suffix;

        public bool IsValidIntermediateKeyId(string keyId)
        {
            return _sessionPartition.IsValidIntermediateKeyId(keyId);
        }
    }
}
